﻿#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used
#pragma warning disable CS0414 // The field is assigned to, but never used
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste {
    public class patch_KeyboardConfigUI : KeyboardConfigUI {

        private enum Mappings {
            Left,
            Right,
            Up,
            Down,
            Jump,
            Dash,
            Grab,
            Talk,
            Confirm,
            Cancel,
            Pause,
            Journal,
            QuickRestart
        }

        private bool remapping;

        private float remappingEase = 0f;

        private float inputDelay = 0f;

        private float timeout;

        private int currentlyRemapping;

        private bool closing;

        private bool additiveRemap;

        [MonoModIgnore]
        private extern string Label(Mappings mapping);

        /// <summary>
        /// Gets the label to display on-screen for a mapping.
        /// </summary>
        /// <param name="mapping">The mapping index</param>
        /// <returns>The key name to display</returns>
        protected virtual string GetLabel(int mapping) {
            // call the vanilla method for this.
            return Label((Mappings) mapping);
        }

        /// <summary>
        /// Adds a key mapping to the keyboard config screen.
        /// </summary>
        /// <param name="key">The mapping index</param>
        /// <param name="list">The list of keys currently mapped to it</param>
        private void AddKeyConfigLine(int key, List<Keys> list) {
            Add(new Setting(GetLabel(key), list).Pressed(() => Remap(key)));
        }

        /// <summary>
        /// Adds a key mapping to the keyboard config screen.
        /// </summary>
        /// <param name="key">The mapping (should be an enum value)</param>
        /// <param name="list">The list of keys currently mapped to it</param>
        protected void AddKeyConfigLine(object key, List<Keys> list) {
            AddKeyConfigLine((int) key, list);
        }

        /// <summary>
        /// Forces a key to be bound to an action, in addition to the already bound key.
        /// </summary>
        /// <param name="defaultKey">The key to force bind</param>
        /// <param name="boundKey">The key already bound</param>
        /// <returns>A list containing both key and defaultKey</returns>
        protected List<Keys> ForceDefaultKey(Keys defaultKey, Keys boundKey) {
            List<Keys> list = new List<Keys> { boundKey };
            if (boundKey != defaultKey)
                list.Add(defaultKey);
            return list;
        }

        /// <summary>
        /// Forces a key to be bound to an action, in addition to already bound keys.
        /// </summary>
        /// <param name="defaultKey">The key to force bind</param>
        /// <param name="boundKeys">The list of keys already bound</param>
        /// <returns>A list containing both keys in list and defaultKey</returns>
        protected List<Keys> ForceDefaultKey(Keys defaultKey, List<Keys> boundKeys) {
            if (!boundKeys.Contains(defaultKey))
                boundKeys.Add(defaultKey);
            return boundKeys;
        }

        /// <summary>
        /// Rebuilds the key mapping menu. Should clear the menu and add back all options.
        /// </summary>
        /// <param name="index">The index to focus on in the menu</param>
        [MonoModReplace]
        public virtual void Reload(int index = -1) {
            Clear();
            Add(new Header(Dialog.Clean("KEY_CONFIG_TITLE")));
            Add(new SubHeader(Dialog.Clean("KEY_CONFIG_ADDITION_HINT")));

            Add(new SubHeader(Dialog.Clean("KEY_CONFIG_MOVEMENT")));
            AddKeyConfigLine(Mappings.Left, ForceDefaultKey(Keys.Left, Settings.Instance.Left));
            AddKeyConfigLine(Mappings.Right, ForceDefaultKey(Keys.Right, Settings.Instance.Right));
            AddKeyConfigLine(Mappings.Up, ForceDefaultKey(Keys.Up, Settings.Instance.Up));
            AddKeyConfigLine(Mappings.Down, ForceDefaultKey(Keys.Down, Settings.Instance.Down));

            Add(new SubHeader(Dialog.Clean("KEY_CONFIG_GAMEPLAY")));
            AddKeyConfigLine(Mappings.Jump, Settings.Instance.Jump);
            AddKeyConfigLine(Mappings.Dash, Settings.Instance.Dash);
            AddKeyConfigLine(Mappings.Grab, Settings.Instance.Grab);
            AddKeyConfigLine(Mappings.Talk, Settings.Instance.Talk);

            Add(new SubHeader(Dialog.Clean("KEY_CONFIG_MENUS")));
            AddKeyConfigLine(Mappings.Confirm, ForceDefaultKey(Keys.Enter, Settings.Instance.Confirm));
            AddKeyConfigLine(Mappings.Cancel, ForceDefaultKey(Keys.Back, Settings.Instance.Cancel));
            AddKeyConfigLine(Mappings.Pause, ForceDefaultKey(Keys.Escape, Settings.Instance.Pause));
            AddKeyConfigLine(Mappings.Journal, Settings.Instance.Journal);
            AddKeyConfigLine(Mappings.QuickRestart, Settings.Instance.QuickRestart);
            Add(new SubHeader(""));

            Button button = new Button(Dialog.Clean("KEY_CONFIG_RESET"));
            button.IncludeWidthInMeasurement = false;
            button.AlwaysCenter = true;
            button.OnPressed = delegate {
                Settings.Instance.SetDefaultKeyboardControls(reset: true);
                Input.Initialize();
                Reload(Selection);
            };
            Add(button);
            if (index >= 0) {
                Selection = index;
            }
        }

        // these keys only support a single mapping (they are not lists in the settings file).
        private static Mappings[] keysWithSingleBindings = { Mappings.Left, Mappings.Right, Mappings.Up, Mappings.Down };

        [MonoModReplace]
        private void Remap(int mapping) {
            remapping = true;
            currentlyRemapping = mapping;
            timeout = 5f;
            Focused = false;
            KeyboardState keyboard = Keyboard.GetState();
            additiveRemap = (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
                && SupportsMultipleBindings(mapping);
        }

        /// <summary>
        /// Determines if the key being set supports multiple bindings.
        /// If this is the case, Shift+Confirm will allow to add a binding and Confirm will replace existing bindings.
        /// </summary>
        /// <param name="mapping">The mapping</param>
        /// <returns>true if the key supports multiple bindings, false otherwise</returns>
        private bool SupportsMultipleBindings(int mapping) {
            return !keysWithSingleBindings.Contains((Mappings) mapping);
        }

        [MonoModReplace]
        private void SetRemap(Keys key) {
            remapping = false;
            inputDelay = 0.25f;
            RemapKey(currentlyRemapping, key, additiveRemap);
            Input.Initialize();
            Reload(Selection);
        }

        /// <summary>
        /// Sets a new binding for a key.
        /// </summary>
        /// <param name="remapping">The mapping that is being edited</param>
        /// <param name="newKey">The key that was just mapped</param>
        /// <param name="addingKey">true if a key binding is being added, false if bindings are getting replaced</param>
        protected virtual void RemapKey(int remapping, Keys newKey, bool addingKey) {
            Mappings remappingKey = (Mappings) remapping;
            if (newKey == Keys.None ||
                (newKey == Keys.Left && remappingKey != Mappings.Left) ||
                (newKey == Keys.Right && remappingKey != Mappings.Right) ||
                (newKey == Keys.Up && remappingKey != Mappings.Up) ||
                (newKey == Keys.Down && remappingKey != Mappings.Down) ||
                (newKey == Keys.Enter && remappingKey != Mappings.Confirm) ||
                (newKey == Keys.Back && remappingKey != Mappings.Cancel)) {
                return;
            }
            List<Keys> keyList = null;
            switch (remappingKey) {
                case Mappings.Left:
                    Settings.Instance.Left = ((newKey != Keys.Left) ? newKey : Keys.None);
                    break;
                case Mappings.Right:
                    Settings.Instance.Right = ((newKey != Keys.Right) ? newKey : Keys.None);
                    break;
                case Mappings.Up:
                    Settings.Instance.Up = ((newKey != Keys.Up) ? newKey : Keys.None);
                    break;
                case Mappings.Down:
                    Settings.Instance.Down = ((newKey != Keys.Down) ? newKey : Keys.None);
                    break;
                case Mappings.Jump:
                    keyList = Settings.Instance.Jump;
                    break;
                case Mappings.Dash:
                    keyList = Settings.Instance.Dash;
                    break;
                case Mappings.Grab:
                    keyList = Settings.Instance.Grab;
                    break;
                case Mappings.Talk:
                    keyList = Settings.Instance.Talk;
                    break;
                case Mappings.Confirm:
                    if (!Settings.Instance.Cancel.Contains(newKey) && !Settings.Instance.Pause.Contains(newKey)) {
                        if (newKey != Keys.Enter) {
                            keyList = Settings.Instance.Confirm;
                        }
                    }
                    break;
                case Mappings.Cancel:
                    if (!Settings.Instance.Confirm.Contains(newKey) && !Settings.Instance.Pause.Contains(newKey)) {
                        if (newKey != Keys.Back) {
                            keyList = Settings.Instance.Cancel;
                        }
                    }
                    break;
                case Mappings.Pause:
                    if (!Settings.Instance.Confirm.Contains(newKey) && !Settings.Instance.Cancel.Contains(newKey)) {
                        keyList = Settings.Instance.Pause;
                    }
                    break;
                case Mappings.Journal:
                    keyList = Settings.Instance.Journal;
                    break;
                case Mappings.QuickRestart:
                    keyList = Settings.Instance.QuickRestart;
                    break;
            }
            if (keyList != null) {
                if (!addingKey)
                    keyList.Clear();
                if (!keyList.Contains(newKey))
                    keyList.Add(newKey);
            }
        }

        [MonoModLinkTo("Celeste.TextMenu", "System.Void Render()")]
        [MonoModRemove]
        private extern void RenderTextMenu();

        [MonoModReplace]
        public override void Render() {
            Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * Ease.CubeOut(Alpha));
            RenderTextMenu();
            if (remappingEase > 0f) {
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.95f * Ease.CubeInOut(remappingEase));
                Vector2 value = new Vector2(1920f, 1080f) * 0.5f;
                ActiveFont.Draw(additiveRemap ? Dialog.Get("KEY_CONFIG_ADDING") : Dialog.Get("KEY_CONFIG_CHANGING"), value + new Vector2(0f, -8f), new Vector2(0.5f, 1f), Vector2.One * 0.7f, Color.LightGray * Ease.CubeIn(remappingEase));
                ActiveFont.Draw(GetLabel(currentlyRemapping), value + new Vector2(0f, 8f), new Vector2(0.5f, 0f), Vector2.One * 2f, Color.White * Ease.CubeIn(remappingEase));
            }
        }
    }
}
