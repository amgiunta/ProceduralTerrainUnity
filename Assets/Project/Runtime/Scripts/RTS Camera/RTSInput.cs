//------------------------------------------------------------------------------
// <auto-generated>
//     This code was auto-generated by com.unity.inputsystem:InputActionCodeGenerator
//     version 1.3.0
//     from Assets/Project/Runtime/Scripts/RTS Camera/RTSInput.inputactions
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace RTSCamera.Input
{
    public partial class @RTSInput : IInputActionCollection2, IDisposable
    {
        public InputActionAsset asset { get; }
        public @RTSInput()
        {
            asset = InputActionAsset.FromJson(@"{
    ""name"": ""RTSInput"",
    ""maps"": [
        {
            ""name"": ""Game"",
            ""id"": ""4ac2c8ce-745b-4800-942e-51a07001f783"",
            ""actions"": [
                {
                    ""name"": ""Point"",
                    ""type"": ""Value"",
                    ""id"": ""82dda96d-5921-440f-b0c3-2d30eec99b4a"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""Select Secondary"",
                    ""type"": ""Button"",
                    ""id"": ""d8c186a0-0ce7-4a36-af93-8bf924b1ddd9"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Select Primary"",
                    ""type"": ""Button"",
                    ""id"": ""781195e4-5b2c-45d8-b1eb-adc338c4d67d"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Delta"",
                    ""type"": ""Value"",
                    ""id"": ""fe520b2c-2378-43b8-95c5-1f3a6b876946"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""Select Terciary"",
                    ""type"": ""Button"",
                    ""id"": ""a50801a4-3d20-4b1b-b317-40aa3c1f9e18"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Zoom"",
                    ""type"": ""Value"",
                    ""id"": ""b0ce4180-ab3d-4387-8b12-5859745cb2cc"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                }
            ],
            ""bindings"": [
                {
                    ""name"": """",
                    ""id"": ""beeefef1-abb1-44f1-87ad-5fc9566e5924"",
                    ""path"": ""<Mouse>/position"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Point"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""e5a40777-8547-4391-afc1-995e966ff7ca"",
                    ""path"": ""<Mouse>/rightButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Select Secondary"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""aa34db64-b6fc-44b4-93d9-ec7c1f472ee1"",
                    ""path"": ""<Mouse>/leftButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Select Primary"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""65f62489-19b9-43c9-85a7-7e1943c27ede"",
                    ""path"": ""<Mouse>/delta"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Delta"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""67755387-3381-4f1c-9b18-651f5a4a0cb9"",
                    ""path"": ""<Mouse>/middleButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Select Terciary"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""efed9ff4-4fe7-4f47-b914-e82a12518b7f"",
                    ""path"": ""<Mouse>/scroll"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Zoom"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        }
    ],
    ""controlSchemes"": []
}");
            // Game
            m_Game = asset.FindActionMap("Game", throwIfNotFound: true);
            m_Game_Point = m_Game.FindAction("Point", throwIfNotFound: true);
            m_Game_SelectSecondary = m_Game.FindAction("Select Secondary", throwIfNotFound: true);
            m_Game_SelectPrimary = m_Game.FindAction("Select Primary", throwIfNotFound: true);
            m_Game_Delta = m_Game.FindAction("Delta", throwIfNotFound: true);
            m_Game_SelectTerciary = m_Game.FindAction("Select Terciary", throwIfNotFound: true);
            m_Game_Zoom = m_Game.FindAction("Zoom", throwIfNotFound: true);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(asset);
        }

        public InputBinding? bindingMask
        {
            get => asset.bindingMask;
            set => asset.bindingMask = value;
        }

        public ReadOnlyArray<InputDevice>? devices
        {
            get => asset.devices;
            set => asset.devices = value;
        }

        public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

        public bool Contains(InputAction action)
        {
            return asset.Contains(action);
        }

        public IEnumerator<InputAction> GetEnumerator()
        {
            return asset.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Enable()
        {
            asset.Enable();
        }

        public void Disable()
        {
            asset.Disable();
        }
        public IEnumerable<InputBinding> bindings => asset.bindings;

        public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
        {
            return asset.FindAction(actionNameOrId, throwIfNotFound);
        }
        public int FindBinding(InputBinding bindingMask, out InputAction action)
        {
            return asset.FindBinding(bindingMask, out action);
        }

        // Game
        private readonly InputActionMap m_Game;
        private IGameActions m_GameActionsCallbackInterface;
        private readonly InputAction m_Game_Point;
        private readonly InputAction m_Game_SelectSecondary;
        private readonly InputAction m_Game_SelectPrimary;
        private readonly InputAction m_Game_Delta;
        private readonly InputAction m_Game_SelectTerciary;
        private readonly InputAction m_Game_Zoom;
        public struct GameActions
        {
            private @RTSInput m_Wrapper;
            public GameActions(@RTSInput wrapper) { m_Wrapper = wrapper; }
            public InputAction @Point => m_Wrapper.m_Game_Point;
            public InputAction @SelectSecondary => m_Wrapper.m_Game_SelectSecondary;
            public InputAction @SelectPrimary => m_Wrapper.m_Game_SelectPrimary;
            public InputAction @Delta => m_Wrapper.m_Game_Delta;
            public InputAction @SelectTerciary => m_Wrapper.m_Game_SelectTerciary;
            public InputAction @Zoom => m_Wrapper.m_Game_Zoom;
            public InputActionMap Get() { return m_Wrapper.m_Game; }
            public void Enable() { Get().Enable(); }
            public void Disable() { Get().Disable(); }
            public bool enabled => Get().enabled;
            public static implicit operator InputActionMap(GameActions set) { return set.Get(); }
            public void SetCallbacks(IGameActions instance)
            {
                if (m_Wrapper.m_GameActionsCallbackInterface != null)
                {
                    @Point.started -= m_Wrapper.m_GameActionsCallbackInterface.OnPoint;
                    @Point.performed -= m_Wrapper.m_GameActionsCallbackInterface.OnPoint;
                    @Point.canceled -= m_Wrapper.m_GameActionsCallbackInterface.OnPoint;
                    @SelectSecondary.started -= m_Wrapper.m_GameActionsCallbackInterface.OnSelectSecondary;
                    @SelectSecondary.performed -= m_Wrapper.m_GameActionsCallbackInterface.OnSelectSecondary;
                    @SelectSecondary.canceled -= m_Wrapper.m_GameActionsCallbackInterface.OnSelectSecondary;
                    @SelectPrimary.started -= m_Wrapper.m_GameActionsCallbackInterface.OnSelectPrimary;
                    @SelectPrimary.performed -= m_Wrapper.m_GameActionsCallbackInterface.OnSelectPrimary;
                    @SelectPrimary.canceled -= m_Wrapper.m_GameActionsCallbackInterface.OnSelectPrimary;
                    @Delta.started -= m_Wrapper.m_GameActionsCallbackInterface.OnDelta;
                    @Delta.performed -= m_Wrapper.m_GameActionsCallbackInterface.OnDelta;
                    @Delta.canceled -= m_Wrapper.m_GameActionsCallbackInterface.OnDelta;
                    @SelectTerciary.started -= m_Wrapper.m_GameActionsCallbackInterface.OnSelectTerciary;
                    @SelectTerciary.performed -= m_Wrapper.m_GameActionsCallbackInterface.OnSelectTerciary;
                    @SelectTerciary.canceled -= m_Wrapper.m_GameActionsCallbackInterface.OnSelectTerciary;
                    @Zoom.started -= m_Wrapper.m_GameActionsCallbackInterface.OnZoom;
                    @Zoom.performed -= m_Wrapper.m_GameActionsCallbackInterface.OnZoom;
                    @Zoom.canceled -= m_Wrapper.m_GameActionsCallbackInterface.OnZoom;
                }
                m_Wrapper.m_GameActionsCallbackInterface = instance;
                if (instance != null)
                {
                    @Point.started += instance.OnPoint;
                    @Point.performed += instance.OnPoint;
                    @Point.canceled += instance.OnPoint;
                    @SelectSecondary.started += instance.OnSelectSecondary;
                    @SelectSecondary.performed += instance.OnSelectSecondary;
                    @SelectSecondary.canceled += instance.OnSelectSecondary;
                    @SelectPrimary.started += instance.OnSelectPrimary;
                    @SelectPrimary.performed += instance.OnSelectPrimary;
                    @SelectPrimary.canceled += instance.OnSelectPrimary;
                    @Delta.started += instance.OnDelta;
                    @Delta.performed += instance.OnDelta;
                    @Delta.canceled += instance.OnDelta;
                    @SelectTerciary.started += instance.OnSelectTerciary;
                    @SelectTerciary.performed += instance.OnSelectTerciary;
                    @SelectTerciary.canceled += instance.OnSelectTerciary;
                    @Zoom.started += instance.OnZoom;
                    @Zoom.performed += instance.OnZoom;
                    @Zoom.canceled += instance.OnZoom;
                }
            }
        }
        public GameActions @Game => new GameActions(this);
        public interface IGameActions
        {
            void OnPoint(InputAction.CallbackContext context);
            void OnSelectSecondary(InputAction.CallbackContext context);
            void OnSelectPrimary(InputAction.CallbackContext context);
            void OnDelta(InputAction.CallbackContext context);
            void OnSelectTerciary(InputAction.CallbackContext context);
            void OnZoom(InputAction.CallbackContext context);
        }
    }
}