using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;

using LitJson;

using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;

using UnityEngine;
using UnityEngine.Networking;

namespace Symphony {
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin {
        internal static new ManualLogSource Logger;
        internal static readonly string VersionTag = "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString();

        internal static IntPtr hWnd => Process.GetCurrentProcess().MainWindowHandle;

        private class GithubReleaseInfo {
            public string tag_name { get; set; }
        }

        public void Awake() {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

            try {
                Enum.GetValues(typeof(ACTOR_CLASS)); // to test game assembly

                StartCoroutine(this.CheckUpdate());

                this.gameObject.AddComponent<WindowedResize>();
            }
            catch {
                Logger.LogError("Failed to find ACTOR_CLASS, seems not installed on LastOrigin or binary changed!");
            }
        }

        private IEnumerator CheckUpdate() {
            var req = UnityWebRequest.Get("https://api.github.com/repos/WolfgangKurz/Symphony/releases/latest");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.ProtocolError || req.result == UnityWebRequest.Result.ConnectionError) {
                Logger.LogError($"[Symphony] Cannot fetch update data: {req.error}");
                yield break;
            }

            try {
                var json = req.downloadHandler.text;
                var tag = JsonMapper.ToObject<GithubReleaseInfo>(json).tag_name;
                if (tag != Plugin.VersionTag) {
                    SceneBase.Instance.ShowMessage(
                        $"Symphony 플러그인에 업데이트가 있습니다.\n새 버전: {tag}\n\nGithub 페이지로 이동하시겠습니까?",
                        "Symphony",
                        "이동하기", "닫기", "",
                        GlobalDefines.MessageType.YESNO, () => {
                            Application.OpenURL("https://github.com/WolfgangKurz/Symphony/releases");
                        }
                    );
                }
            }
            catch (Exception e) {
                Logger.LogError($"[Symphony] Cannot fetch update data: {e.ToString()}");
                yield break;
            }

            yield break;
        }
    }
}