using System;
using BepInEx;
using BepInEx.Configuration;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StonewardsHostPause
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class HostPausePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dev.thereisnouser.stonewards.hostpause";
        public const string PluginName = "Stonewards Host Pause";
        public const string PluginVersion = "0.1.0";

        private const float RefreshPeriodSeconds = 3f;

        private ConfigEntry<KeyboardShortcut> _pauseKey;
        private ConfigEntry<bool> _showHostIndicator;
        private bool _paused;
        private NetworkHelper _networkHelperAtPause;
        private float _timeScaleBeforePause = 1f;
        private float _nextRefreshTime;
        private GUIStyle _indicatorStyle;

        private static bool IsHost => NetworkServer.active && NetworkClient.active;

        private void Awake()
        {
            _pauseKey = Config.Bind("Controls", "PauseKey",
                new KeyboardShortcut(KeyCode.F9),
                "Host-only key to pause or resume the run. All clients receive the game's native RPC.");
            _showHostIndicator = Config.Bind("Appearance", "ShowHostIndicator", true,
                "Show a text reminder on the host's screen while paused. Guests do not need this mod and cannot receive modded UI.");
            Logger.LogInfo("Host Pause loaded. Host can use F9 to pause all connected players using the native NetworkHelper.RpcChangeTimeScale RPC.");
        }

        private void Update()
        {
            var helper = NetworkHelper.Instance;
            bool host = IsHost;
            bool activeRun = host && helper != null &&
                GameManager.Instance != null && GameManager.Instance.LocalPlayer != null;

            // Never leave local time frozen if the network object or the hosting session disappears.
            if (_paused && (!activeRun || helper != _networkHelperAtPause))
            {
                Resume(helper == _networkHelperAtPause && host);
                return;
            }

            if (_paused)
            {
                // Stonewards changes its time scale during gameplay phase transitions.
                // Do not fight the game's upgrade/end-of-wave phases.
                if (helper.NetworkblockingState != NetworkHelper.BlockingState.None)
                {
                    ReleaseForGamePhase();
                    return;
                }

                // RPCs are not buffered for late joiners. Reapply a tiny, infrequent native
                // RPC so an unmodified client that becomes ready mid-pause also stops.
                if (Time.unscaledTime >= _nextRefreshTime)
                {
                    helper.RpcChangeTimeScale(0f);
                    _nextRefreshTime = Time.unscaledTime + RefreshPeriodSeconds;
                }
            }

            if (!activeRun || !WasPressed(_pauseKey.Value))
                return;

            if (_paused)
            {
                Resume(sendRpc: true);
                return;
            }

            if (Time.timeScale <= 0f ||
                helper.NetworkblockingState != NetworkHelper.BlockingState.None ||
                InputManager.Instance == null ||
                InputManager.Instance.CurrentInputState != InputManager.InputState.Gameplay)
            {
                Logger.LogInfo("Pause ignored outside active gameplay or during a game-controlled blocking phase.");
                return;
            }

            Pause(helper);
        }

        private void Pause(NetworkHelper helper)
        {
            if (_paused || !IsHost || helper == null)
                return;

            _networkHelperAtPause = helper;
            _timeScaleBeforePause = Time.timeScale;
            _paused = true;
            _nextRefreshTime = Time.unscaledTime + RefreshPeriodSeconds;
            try
            {
                // Vanilla RPC: clients do NOT need BepInEx or the Host Pause plugin.
                helper.RpcChangeTimeScale(0f);
                Time.timeScale = 0f;
                Logger.LogInfo("Gameplay PAUSED by host.");
            }
            catch (Exception e)
            {
                Logger.LogError("Failed to pause over the game's native RPC: " + e);
                Resume(sendRpc: true);
            }
        }

        // The game's own transition hook already changes timeScale on each client.
        // Do not send a resume RPC over its normal upgrade/menu time control.
        private void ReleaseForGamePhase()
        {
            _paused = false;
            _networkHelperAtPause = null;
            Logger.LogInfo("Host pause released to Stonewards game-phase controls.");
        }

        private void Resume(bool sendRpc)
        {
            if (!_paused)
                return;

            var helper = _networkHelperAtPause;
            _paused = false;
            _networkHelperAtPause = null;
            float targetScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
            try
            {
                if (sendRpc && IsHost && helper != null)
                    helper.RpcChangeTimeScale(targetScale);
            }
            catch (Exception e)
            {
                Logger.LogWarning("Could not broadcast resume (session may be ending): " + e.Message);
            }
            finally
            {
                Time.timeScale = targetScale;
                Logger.LogInfo("Gameplay RESUMED (time scale " + targetScale + ").");
            }
        }

        // Stonewards uses Unity's new Input System; KeyboardShortcut.IsDown() can fail
        // if the old Input Manager is disabled. Read the configured keys from InputSystem.
        private static bool WasPressed(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None || Keyboard.current == null)
                return false;
            if (!TryMapKey(shortcut.MainKey, out var key) ||
                !Keyboard.current[key].wasPressedThisFrame)
                return false;

            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!TryMapKey(modifier, out var mod) || !Keyboard.current[mod].isPressed)
                    return false;
            }
            return true;
        }

        private static bool TryMapKey(KeyCode code, out Key key)
        {
            switch (code)
            {
                case KeyCode.Return: key = Key.Enter; return true;
                case KeyCode.KeypadEnter: key = Key.NumpadEnter; return true;
                case KeyCode.LeftControl: key = Key.LeftCtrl; return true;
                case KeyCode.RightControl: key = Key.RightCtrl; return true;
                case KeyCode.LeftCommand: key = Key.LeftMeta; return true;
                case KeyCode.RightCommand: key = Key.RightMeta; return true;
                case KeyCode.BackQuote: key = Key.Backquote; return true;
                default:
                    string name = code.ToString();
                    if (name.StartsWith("Alpha", StringComparison.Ordinal))
                        name = "Digit" + name.Substring(5);
                    else if (name.StartsWith("Keypad", StringComparison.Ordinal))
                        name = "Numpad" + name.Substring(6);
                    return Enum.TryParse(name, true, out key) && key != Key.None;
            }
        }

        private void OnGUI()
        {
            if (!_paused || !_showHostIndicator.Value || !IsHost)
                return;

            if (_indicatorStyle == null)
            {
                _indicatorStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
            }
            GUI.Label(new Rect((Screen.width - 360f) * 0.5f, 24f, 360f, 40f),
                "PAUSED BY HOST  (F9)", _indicatorStyle);
        }

        private void OnDisable() => Resume(sendRpc: true);
        private void OnDestroy() => Resume(sendRpc: true);
    }
}
