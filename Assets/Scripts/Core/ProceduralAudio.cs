using UnityEngine;

namespace TeaMist.Core
{
    /// <summary>
    /// 程序化音频生成器 —— 运行时生成占位音效/环境音/BGM。
    /// 当 Inspector 未配置实际 AudioClip 时，自动生成简单的正弦波/噪声片段。
    /// 策划后续可替换为真实音频资源。
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        /// <summary>生成正弦波音调（用于门铃、提示音）</summary>
        public static AudioClip CreateSineTone(string name, float frequency, float duration,
            float fadeOut = 0.3f, float volume = 0.5f)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            int fadeSamples = (int)(SampleRate * fadeOut);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = 1f;
                // 淡入
                if (i < SampleRate * 0.01f)
                    envelope = i / (SampleRate * 0.01f);
                // 淡出
                if (i > samples - fadeSamples)
                    envelope = (float)(samples - i) / fadeSamples;

                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * envelope;
            }

            return CreateClip(name, data);
        }

        /// <summary>生成双音门铃</summary>
        public static AudioClip CreateDoorBell()
        {
            float duration = 0.6f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-t * 4f); // 快速衰减
                // 两个频率叠加：叮 + 咚
                float sample = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.4f;
                sample += Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.2f;
                data[i] = sample * envelope;
            }

            return CreateClip("Procedural_DoorBell", data);
        }

        /// <summary>生成倒水声（滤波噪声）</summary>
        public static AudioClip CreateWaterPour(float duration = 1.5f)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            // 简单噪声 + 低通滤波模拟水声
            float prev = 0f;
            float cutoff = 0.15f; // 低通系数

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                // 包络：渐入渐出
                float envelope = Mathf.Sin(Mathf.PI * t / duration) * 0.3f;
                // 白噪声
                float noise = Random.Range(-1f, 1f);
                // 一阶低通滤波
                prev = prev + cutoff * (noise - prev);
                data[i] = prev * envelope;
            }

            return CreateClip("Procedural_WaterPour", data);
        }

        /// <summary>生成碎片获得提示音（上升琶音）</summary>
        public static AudioClip CreateFragmentGet()
        {
            float duration = 0.8f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            float[] freqs = { 523f, 659f, 784f, 1047f }; // C5 E5 G5 C6
            float noteLen = duration / freqs.Length;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                int noteIdx = Mathf.Min((int)(t / noteLen), freqs.Length - 1);
                float noteT = t - noteIdx * noteLen;
                float envelope = Mathf.Exp(-noteT * 6f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freqs[noteIdx] * t) * 0.35f * envelope;
            }

            return CreateClip("Procedural_FragmentGet", data);
        }

        /// <summary>生成环境音：鸟鸣（白天）</summary>
        public static AudioClip CreateAmbientBirds(float duration = 8f)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            // 底层：柔和的粉红噪声（模拟风/环境底噪）
            float prev = 0f;
            for (int i = 0; i < samples; i++)
            {
                float noise = Random.Range(-1f, 1f);
                prev = prev + 0.02f * (noise - prev);
                data[i] = prev * 0.05f;
            }

            // 叠加：随机鸟鸣（短促的高频音调）
            int birdCount = 6;
            for (int b = 0; b < birdCount; b++)
            {
                int startSample = Random.Range(0, samples - SampleRate);
                float birdFreq = Random.Range(2000f, 4000f);
                int birdLen = Random.Range(SampleRate / 8, SampleRate / 4);

                for (int i = 0; i < birdLen && startSample + i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float envelope = Mathf.Sin(Mathf.PI * i / birdLen) * 0.12f;
                    // 颤音
                    float vibrato = 1f + Mathf.Sin(2f * Mathf.PI * 20f * t) * 0.1f;
                    data[startSample + i] += Mathf.Sin(2f * Mathf.PI * birdFreq * vibrato * t) * envelope;
                }
            }

            return CreateClip("Procedural_AmbientBirds", data);
        }

        /// <summary>生成环境音：虫鸣（傍晚）</summary>
        public static AudioClip CreateAmbientCrickets(float duration = 8f)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            // 底层：安静底噪
            for (int i = 0; i < samples; i++)
            {
                float noise = Random.Range(-1f, 1f);
                data[i] = noise * 0.01f;
            }

            // 虫鸣：高频脉冲
            int cricketCount = 3;
            for (int c = 0; c < cricketCount; c++)
            {
                float cricketFreq = Random.Range(3500f, 5000f);
                float pulseRate = Random.Range(15f, 30f);
                float pan = Random.Range(-0.5f, 0.5f);

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    // 脉冲包络
                    float pulse = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * pulseRate * t));
                    pulse *= pulse; // 更尖锐的脉冲
                    float sample = Mathf.Sin(2f * Mathf.PI * cricketFreq * t) * 0.06f * pulse;
                    data[i] += sample;
                }
            }

            return CreateClip("Procedural_AmbientCrickets", data);
        }

        /// <summary>生成环境音：夜间寂静</summary>
        public static AudioClip CreateAmbientNight(float duration = 8f)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            // 极轻的低频风噪
            float prev = 0f;
            for (int i = 0; i < samples; i++)
            {
                float noise = Random.Range(-1f, 1f);
                prev = prev + 0.005f * (noise - prev);
                float t = (float)i / SampleRate;
                float breath = Mathf.Sin(2f * Mathf.PI * 0.1f * t) * 0.5f + 0.5f;
                data[i] = prev * 0.03f * breath;
            }

            // 偶尔一声远处的虫叫
            int chirpStart = Random.Range(samples / 3, samples * 2 / 3);
            int chirpLen = SampleRate / 5;
            for (int i = 0; i < chirpLen && chirpStart + i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Sin(Mathf.PI * i / chirpLen) * 0.04f;
                data[chirpStart + i] += Mathf.Sin(2f * Mathf.PI * 2800f * t) * env;
            }

            return CreateClip("Procedural_AmbientNight", data);
        }

        /// <summary>生成环境音：雨声</summary>
        public static AudioClip CreateAmbientRain(float duration = 8f)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            // 持续噪声（模拟雨声）
            float prev1 = 0f, prev2 = 0f;
            for (int i = 0; i < samples; i++)
            {
                float noise = Random.Range(-1f, 1f);
                // 两级低通 → 柔和的雨声
                prev1 = prev1 + 0.08f * (noise - prev1);
                prev2 = prev2 + 0.12f * (prev1 - prev2);
                data[i] = prev2 * 0.25f;
            }

            // 偶尔的大滴雨声
            for (int d = 0; d < 8; d++)
            {
                int dropStart = Random.Range(0, samples - SampleRate / 4);
                int dropLen = Random.Range(SampleRate / 20, SampleRate / 10);
                float dropFreq = Random.Range(800f, 2000f);

                for (int i = 0; i < dropLen && dropStart + i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float env = Mathf.Exp(-t * 15f) * 0.15f;
                    data[dropStart + i] += Mathf.Sin(2f * Mathf.PI * dropFreq * t) * env;
                }
            }

            return CreateClip("Procedural_AmbientRain", data);
        }

        /// <summary>生成简单 BGM（五声音阶循环）</summary>
        public static AudioClip CreateSimpleBGM(string name, float duration = 16f, float baseFreq = 262f)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            // 五声音阶：宫商角徵羽 (C D E G A)
            float[] pentatonic = { baseFreq, baseFreq * 9f / 8f, baseFreq * 5f / 4f,
                                   baseFreq * 3f / 2f, baseFreq * 5f / 3f };

            // 底层：持续的低音 drone
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float drone = Mathf.Sin(2f * Mathf.PI * baseFreq * 0.5f * t) * 0.06f;
                drone += Mathf.Sin(2f * Mathf.PI * baseFreq * 0.75f * t) * 0.03f;
                data[i] = drone;
            }

            // 随机音符（稀疏的旋律）
            float noteInterval = 1.5f; // 每 1.5 秒一个音符
            int notesCount = (int)(duration / noteInterval);

            for (int n = 0; n < notesCount; n++)
            {
                if (Random.value > 0.6f) continue; // 40% 概率休止

                int noteIdx = Random.Range(0, pentatonic.Length);
                float freq = pentatonic[noteIdx] * (Random.value > 0.7f ? 2f : 1f); // 偶尔高八度
                int startSample = (int)(n * noteInterval * SampleRate) + Random.Range(0, SampleRate / 4);
                int noteSamples = (int)(SampleRate * Random.Range(0.8f, 2f));

                for (int i = 0; i < noteSamples && startSample + i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    // ADSR 简化：慢起快落
                    float envelope = Mathf.Exp(-t * 1.5f) * 0.12f;
                    if (i < SampleRate / 10) envelope *= i / (SampleRate / 10f); // 淡入
                    // 正弦 + 轻微泛音
                    float sample = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope;
                    sample += Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * envelope * 0.15f;
                    data[startSample + i] += sample;
                }
            }

            return CreateClip(name, data);
        }

        /// <summary>生成关门声</summary>
        public static AudioClip CreateDoorClose()
        {
            float duration = 0.3f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-t * 12f);
                // 低频撞击 + 噪声
                float sample = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.3f;
                sample += Random.Range(-1f, 1f) * 0.15f * Mathf.Exp(-t * 20f);
                data[i] = sample * envelope;
            }

            return CreateClip("Procedural_DoorClose", data);
        }

        /// <summary>生成 UI 点击音</summary>
        public static AudioClip CreateUIClick()
        {
            return CreateSineTone("Procedural_UIClick", 660f, 0.1f, 0.05f, 0.2f);
        }

        // ━━━ 工具 ━━━

        private static AudioClip CreateClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            // AudioClip.SetData 需要 float[] 数据
            // Unity 的 AudioClip.Create + SetData 是标准做法
            // 但由于 AudioClip.SetData 在非主线程可能有问题，这里假设在主线程调用
            clip.SetData(data, 0);
            return clip;
        }
    }
}
