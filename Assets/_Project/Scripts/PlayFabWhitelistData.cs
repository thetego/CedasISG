using UnityEngine;

namespace SafetyTraining
{
    [CreateAssetMenu(fileName = "PlayFabWhitelist", menuName = "Safety Training/PlayFab Whitelist")]
    public class PlayFabWhitelistData : ScriptableObject
    {
        [Header("━━━ OYUNCU LİSTESİ ━━━")]
        public PlayerEntry[] players;

        [System.Serializable]
        public class PlayerEntry
        {
            [Tooltip("PlayFab'a gönderilecek benzersiz ID")]
            public string playerId;

            [Tooltip("Listede görünecek ad")]
            public string displayName;

            [Tooltip("Rol (worker / inspector / trainee)")]
            public string role = "trainee";
        }

        public bool Contains(string playerId)
        {
            if (players == null) return false;
            foreach (var p in players)
                if (p.playerId == playerId) return true;
            return false;
        }

        public PlayerEntry GetEntry(string playerId)
        {
            if (players == null) return null;
            foreach (var p in players)
                if (p.playerId == playerId) return p;
            return null;
        }
    }
}
