using UnityEngine;
using UnityEngine.EventSystems;
using Il2Cpp;
using FFV_ScreenReader.Utils;
using MelonLoader;
using Il2CppSerial.FF5.UI.KeyInput;
using FFV_ScreenReader.Menus;

namespace FFV_ScreenReader.Core
{
    public class InputManager
    {
        private readonly FFV_ScreenReaderMod mod;
        private StatusDetailsController cachedStatusController;

        public InputManager(FFV_ScreenReaderMod mod)
        {
            this.mod = mod;
        }
        
        public void Update()
        {
            if (!Input.anyKeyDown)
            {
                return;
            }
            
            if (IsInputFieldFocused())
            {
                return;
            }
            
            bool statusScreenActive = IsStatusScreenActive();

            if (statusScreenActive)
            {
                HandleStatusScreenInput();
            }
            else
            {
                HandleFieldInput();
            }
            
            HandleGlobalInput();
        }
        
        private bool IsInputFieldFocused()
        {
            try
            {
                if (EventSystem.current == null)
                    return false;

                var currentObj = EventSystem.current.currentSelectedGameObject;
                
                if (currentObj == null)
                    return false;
                
                return currentObj.TryGetComponent(out UnityEngine.UI.InputField inputField);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Error checking input field state: {ex.Message}");
                return false;
            }
        }
        
        private bool IsStatusScreenActive()
        {
            if (cachedStatusController == null || cachedStatusController.gameObject == null)
            {
                cachedStatusController = GameObjectCache.Get<StatusDetailsController>();
            }

            return cachedStatusController != null &&
                   cachedStatusController.gameObject != null &&
                   cachedStatusController.gameObject.activeInHierarchy;
        }
        
        private void HandleStatusScreenInput()
        {
            if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.LeftBracket))
            {
                string physicalStats = StatusDetailsReader.ReadPhysicalStats();
                FFV_ScreenReaderMod.SpeakText(physicalStats);
            }

            if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.RightBracket))
            {
                string magicalStats = StatusDetailsReader.ReadMagicalStats();
                FFV_ScreenReaderMod.SpeakText(magicalStats);
            }
        }
        
        private void HandleFieldInput()
        {
            if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.LeftBracket))
            {
                if (IsShiftHeld())
                {
                    mod.CyclePreviousCategory();
                }
                else
                {
                    mod.CyclePrevious();
                }
            }
            
            if (Input.GetKeyDown(KeyCode.K))
            {
                mod.AnnounceEntityOnly();
            }
            
            if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.RightBracket))
            {
                if (IsShiftHeld())
                {
                    mod.CycleNextCategory();
                }
                else
                {
                    mod.CycleNext();
                }
            }
            
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Backslash))
            {
                if (IsShiftHeld())
                {
                    mod.TogglePathfindingFilter();
                }
                else
                {
                    mod.AnnounceCurrentEntity();
                }
            }
        }
        
        private void HandleGlobalInput()
        {
            if (IsCtrlHeld())
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    mod.TeleportInDirection(new Vector2(0, 16)); // North
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    mod.TeleportInDirection(new Vector2(0, -16)); // South
                }
                else if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    mod.TeleportInDirection(new Vector2(-16, 0)); // West
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    mod.TeleportInDirection(new Vector2(16, 0)); // East
                }
            }
            
            if (Input.GetKeyDown(KeyCode.H))
            {
                mod.AnnounceAirshipOrCharacterStatus();
            }
            
            if (Input.GetKeyDown(KeyCode.G))
            {
                mod.AnnounceGilAmount();
            }
            
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (IsShiftHeld())
                {
                    mod.ToggleMapExitFilter();
                }
                else
                {
                    mod.AnnounceCurrentMap();
                }
            }
            
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                mod.ResetToAllCategory();
            }

            if (Input.GetKeyDown(KeyCode.K) && IsShiftHeld())
            {
                mod.ResetToAllCategory();
            }
            
            if (Input.GetKeyDown(KeyCode.Equals))
            {
                mod.CycleNextCategory();
            }
            
            if (Input.GetKeyDown(KeyCode.Minus))
            {
                mod.CyclePreviousCategory();
            }
            
            if (Input.GetKeyDown(KeyCode.T))
            {
            }
        }
        
        private bool IsShiftHeld()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
        
        private bool IsCtrlHeld()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }
    }
}
