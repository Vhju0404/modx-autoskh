using System;
using System.Text;

namespace AssemblyCSharp.Mod
{
    /// <summary>
    /// Thong tin thuong hieu MOD X - H TEAM va ma xac thuc dong bo phien noi bo (127.0.0.1).
    /// Ma nguon minh bach 100%, khong thu thap du lieu hay ket noi mang ngoai.
    /// </summary>
    internal static class NetProfileMap
    {
        private const string BrandTitle = "MOD X";
        private const string BrandTeam = "H TEAM";
        private const string HudFull = "MOD X | H TEAM";
        private const string HudShort = "MOD X";
        private const string VerifyText = "[MOD X · H TEAM] Ban goc chinh chu";
        private const string SessionKey = "HX9#2026!HTEAM@MODX";
        private const ulong ExpectedCoreHash = 0x260A4B9ED4A52607UL;

        private static bool _peerAcked;
        private static string _peerTag = "";
        private static long _connectedSinceMs = 0;

        public static bool IsSelfValid()
        {
            return Fnv64(BrandTitle + "|" + BrandTeam + "|" + HudFull + "|" + SessionKey) == ExpectedCoreHash;
        }

        public static string HudFullPrefix()
        {
            if (!string.IsNullOrEmpty(_peerTag) && _peerAcked && IsSelfValid())
                return _peerTag;
            return HudFull;
        }

        public static string HudShortPrefix()
        {
            return HudShort;
        }

        public static string VerifyBanner()
        {
            ulong h = Fnv64(BrandTitle + "|" + BrandTeam + "|" + SessionKey);
            return VerifyText + " | ID:" + (h & 0xFFFFFFFFUL).ToString("X8");
        }

        public static string ComputeClientSig(string accountId)
        {
            if (!IsSelfValid()) return "0000000000000000";
            string id = (accountId ?? "").Trim();
            return Fnv64(BrandTitle + ":" + BrandTeam + ":" + SessionKey + ":C:" + id).ToString("x16");
        }

        public static void MarkSessionBound(long nowMs)
        {
            _connectedSinceMs = nowMs;
            _peerAcked = false;
        }

        public static bool ApplyServerSync(string accountId, string ack, string tag)
        {
            string id = (accountId ?? "").Trim();
            string exp = Fnv64(BrandTeam + ":" + BrandTitle + ":" + SessionKey + ":S:" + id).ToString("x16");
            if (string.Equals(ack, exp, StringComparison.OrdinalIgnoreCase) && IsSelfValid())
            {
                _peerAcked = true;
                if (!string.IsNullOrEmpty(tag)) _peerTag = tag;
                return true;
            }
            _peerAcked = false;
            return false;
        }

        public static bool IsPeerSyncTimedOut(long nowMs)
        {
            if (!IsSelfValid()) return true;
            // Chi ap dung khi duoc mo tu trinh quan ly QLTK (co accountID) va qua 20s khong co ack hop le
            if (string.IsNullOrEmpty(LoadData.Account.accountID)) return false;
            if (!SocketClient.IsConnected) return false;
            if (_peerAcked) return false;
            return _connectedSinceMs > 0 && (nowMs - _connectedSinceMs) > 20000L;
        }

        private static ulong Fnv64(string s)
        {
            byte[] data = Encoding.UTF8.GetBytes(s ?? "");
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < data.Length; i++)
            {
                h ^= data[i];
                h *= 1099511628211UL;
            }
            return h;
        }
    }
}
