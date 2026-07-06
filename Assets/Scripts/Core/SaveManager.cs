using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using TeaMist.Data;

namespace TeaMist.Core
{
    /// <summary>
    /// 存档管理器 — 双轨存档系统
    /// - PlayerPrefs：轻量设置（音量、语言、最后存档槽位）
    /// - JSON 文件：完整游戏存档（多槽位，带版本迁移）
    /// </summary>
    public static class SaveManager
    {
        private const int MAX_SAVE_SLOTS = 5;
        private const string SAVE_DIR = "Saves";
        private const string SAVE_PREFIX = "tea_mist_save_";
        private const string SAVE_EXT = ".json";
        private const string SETTINGS_KEY_PREFIX = "TeaMist_Settings_";

        private static string SaveDirectory =>
            Path.Combine(Application.persistentDataPath, SAVE_DIR);

        // ── 公共 API ──

        /// <summary>保存到指定槽位</summary>
        public static bool Save(SaveData data, int slot)
        {
            if (slot < 1 || slot > MAX_SAVE_SLOTS)
            {
                Debug.LogError($"[SaveManager] 非法槽位: {slot}");
                return false;
            }

            try
            {
                data.saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                data.saveVersion = 1;

                if (!Directory.Exists(SaveDirectory))
                    Directory.CreateDirectory(SaveDirectory);

                string json = JsonUtility.ToJson(data, true);
                string path = GetSlotPath(slot);
                File.WriteAllText(path, json);

                // 更新设置
                PlayerPrefs.SetString(SETTINGS_KEY_PREFIX + "LastSlot", slot.ToString());
                PlayerPrefs.SetString(SETTINGS_KEY_PREFIX + "LastSaveTime", data.saveTimestamp);
                PlayerPrefs.Save();

                Debug.Log($"[SaveManager] 存档成功 — 槽位 {slot}: {data.saveName}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 存档失败: {e.Message}");
                return false;
            }
        }

        /// <summary>从指定槽位读取</summary>
        public static SaveData Load(int slot)
        {
            string path = GetSlotPath(slot);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SaveManager] 槽位 {slot} 没有存档");
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                // 版本迁移
                if (data.saveVersion < 1)
                {
                    MigrateSave(data);
                }

                Debug.Log($"[SaveManager] 读档成功 — 槽位 {slot}: {data.saveName}");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 读档失败: {e.Message}");
                return null;
            }
        }

        /// <summary>获取某个槽位的存档元数据（不完整加载）</summary>
        public static SaveSlotInfo GetSlotInfo(int slot)
        {
            string path = GetSlotPath(slot);
            if (!File.Exists(path))
                return new SaveSlotInfo { slot = slot, isEmpty = true };

            try
            {
                string json = File.ReadAllText(path);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                return new SaveSlotInfo
                {
                    slot = slot,
                    isEmpty = false,
                    saveName = data.saveName,
                    timestamp = data.saveTimestamp,
                    totalDays = data.totalDaysPlayed,
                    currentSeason = data.currentSeason,
                    playerName = data.player?.playerName ?? "掌柜"
                };
            }
            catch
            {
                return new SaveSlotInfo { slot = slot, isEmpty = true, saveName = "(损坏)" };
            }
        }

        /// <summary>获取所有槽位信息</summary>
        public static List<SaveSlotInfo> GetAllSlotInfos()
        {
            var list = new List<SaveSlotInfo>();
            for (int i = 1; i <= MAX_SAVE_SLOTS; i++)
                list.Add(GetSlotInfo(i));
            return list;
        }

        /// <summary>删除指定槽位</summary>
        public static bool DeleteSlot(int slot)
        {
            string path = GetSlotPath(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"[SaveManager] 已删除槽位 {slot}");
                return true;
            }
            return false;
        }

        /// <summary>是否存在任何存档</summary>
        public static bool HasAnySave()
        {
            for (int i = 1; i <= MAX_SAVE_SLOTS; i++)
                if (File.Exists(GetSlotPath(i)))
                    return true;
            return false;
        }

        /// <summary>获取最后使用的槽位</summary>
        public static int GetLastUsedSlot()
        {
            return PlayerPrefs.GetInt(SETTINGS_KEY_PREFIX + "LastSlot", 1);
        }

        // ── 设置存取 ──

        public static void SaveSetting(string key, string value)
        {
            PlayerPrefs.SetString(SETTINGS_KEY_PREFIX + key, value);
            PlayerPrefs.Save();
        }

        public static string LoadSetting(string key, string defaultValue = "")
        {
            return PlayerPrefs.GetString(SETTINGS_KEY_PREFIX + key, defaultValue);
        }

        public static void SaveSettingFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(SETTINGS_KEY_PREFIX + key, value);
            PlayerPrefs.Save();
        }

        public static float LoadSettingFloat(string key, float defaultValue = 0f)
        {
            return PlayerPrefs.GetFloat(SETTINGS_KEY_PREFIX + key, defaultValue);
        }

        public static void SaveSettingInt(string key, int value)
        {
            PlayerPrefs.SetInt(SETTINGS_KEY_PREFIX + key, value);
            PlayerPrefs.Save();
        }

        public static int LoadSettingInt(string key, int defaultValue = 0)
        {
            return PlayerPrefs.GetInt(SETTINGS_KEY_PREFIX + key, defaultValue);
        }

        /// <summary>获取槽位文件路径（公开，供 UI 检查是否存在）</summary>
        public static string GetSlotPathPublic(int slot) => GetSlotPath(slot);

        // ── 私有 ──

        private static string GetSlotPath(int slot) =>
            Path.Combine(SaveDirectory, $"{SAVE_PREFIX}{slot}{SAVE_EXT}");

        private static void MigrateSave(SaveData data)
        {
            // 未来版本迁移逻辑在此
            Debug.Log($"[SaveManager] 存档版本迁移: {data.saveVersion} → 1");
            data.saveVersion = 1;
        }
    }

    /// <summary>存档槽位摘要（用于 UI 展示）</summary>
    [System.Serializable]
    public struct SaveSlotInfo
    {
        public int slot;
        public bool isEmpty;
        public string saveName;
        public string timestamp;
        public int totalDays;
        public Season currentSeason;
        public string playerName;
    }
}
