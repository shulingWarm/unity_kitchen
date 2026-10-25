using UnityEngine;

namespace RollABall
{
    /// <summary>主菜单"选择角色"的存档：跨场景读取所选形象（PlayerPrefs 持久化）。</summary>
    public static class CharacterSelection
    {
        public const string ChibiBoy = "ChibiBoy";
        public const string Cat = "Cat";
        public const string Girl = "Girl";
        public const string Xiaoke = "Xiaoke";
        public const string Mouse = "Mouse";
        public const string Default = "Default";   // X Bot

        private const string PrefKey = "SelectedCharacter";

        public static string Get() => PlayerPrefs.GetString(PrefKey, "");
        public static void Set(string id) => PlayerPrefs.SetString(PrefKey, id);
    }
}
