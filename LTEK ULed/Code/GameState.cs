using Avalonia.Threading;
using LTEK_ULed.Views;
using System;

namespace LTEK_ULed.Code
{
    public class GameState
    {
        public static GameState gameState = new GameState();

        public delegate void ParsedData();

        public event ParsedData OnParsedData;

        public readonly State state = new State();

        public bool Connected {
            get;
            protected set;
        }

        public void Parse(byte[] data)
        {

            CabinetLight cabinetLight = (CabinetLight)(data[0] & 0b0111111);

            LightsMode lightsMode = (LightsMode)data[0];

            int buffer = ((int)data[6] & 0b001111) |
                         (((int)data[5] & 0b001111) << 6) |
                         (((int)data[4] & 0b001111) << 11) |
                         (((int)data[3] & 0b001111) << 17) |
                         (((int)data[2] & 0b001111) << 23) |
                         (((int)data[1] & 0b001111) << 29);

            GameButton gameButton = 0;

            gameButton = gameButton |
                ((data[1] & (1 << 0)) != 0 ? GameButton.GAME_BUTTON_MENULEFT : 0) |
                ((data[1] & (1 << 1)) != 0 ? GameButton.GAME_BUTTON_MENURIGHT : 0) |
                ((data[1] & (1 << 2)) != 0 ? GameButton.GAME_BUTTON_MENUUP : 0) |
                ((data[1] & (1 << 3)) != 0 ? GameButton.GAME_BUTTON_MENUDOWN : 0) |
                ((data[1] & (1 << 4)) != 0 ? GameButton.GAME_BUTTON_START : 0) |
                ((data[1] & (1 << 5)) != 0 ? GameButton.GAME_BUTTON_SELECT : 0);

            gameButton = gameButton |
                ((data[2] & (1 << 0)) != 0 ? GameButton.GAME_BUTTON_BACK : 0) |
                ((data[2] & (1 << 1)) != 0 ? GameButton.GAME_BUTTON_COIN : 0) |
                ((data[2] & (1 << 2)) != 0 ? GameButton.GAME_BUTTON_OPERATOR : 0) |
                ((data[2] & (1 << 3)) != 0 ? GameButton.GAME_BUTTON_EFFECT_UP : 0) |
                ((data[2] & (1 << 4)) != 0 ? GameButton.GAME_BUTTON_EFFECT_DOWN : 0);

            gameButton = gameButton |
                ((data[3] & (1 << 0)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_01 : 0) |
                ((data[3] & (1 << 1)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_02 : 0) |
                ((data[3] & (1 << 2)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_03 : 0) |
                ((data[3] & (1 << 3)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_04 : 0) |
                ((data[3] & (1 << 4)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_05 : 0) |
                ((data[3] & (1 << 5)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_06 : 0);

            gameButton = gameButton |
                ((data[4] & (1 << 0)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_07 : 0) |
                ((data[4] & (1 << 1)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_08 : 0) |
                ((data[4] & (1 << 2)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_09 : 0) |
                ((data[4] & (1 << 3)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_10 : 0) |
                ((data[4] & (1 << 4)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_11 : 0) |
                ((data[4] & (1 << 5)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_12 : 0);

            gameButton = gameButton |
                ((data[5] & (1 << 0)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_13 : 0) |
                ((data[5] & (1 << 1)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_14 : 0) |
                ((data[5] & (1 << 2)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_15 : 0) |
                ((data[5] & (1 << 3)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_16 : 0) |
                ((data[5] & (1 << 4)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_17 : 0) |
                ((data[5] & (1 << 5)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_18 : 0);

            gameButton = gameButton |
                ((data[6] & (1 << 0)) != 0 ? GameButton.GAME_BUTTON_CUSTOM_19 : 0);

            //for(int i = 1; i < 7; i++)
            //{
            //    Debug.Write(Convert.ToString(data[i],2).PadLeft(8, '0'));
            //}
            //Debug.WriteLine("");

            lock (state)
            {
                state.cabinetLight = cabinetLight;
                state.lightsMode = lightsMode;
                state.gameButton = gameButton;
            }

            Dispatcher.UIThread.Post(MainWindow.Instance.UpdateUi);

        }

        public void SetConnectionStatus(bool connected)
        {
            this.Connected = connected;
        }
    }


    public class State
    {
        public LightsMode lightsMode;

        public CabinetLight cabinetLight;

        public GameButton gameButton;

        public uint p1Combo;
        public float p1Precision;

        public uint p2Combo;
        public float p2Precision;
    }

    public enum LightsMode
    {
        LIGHTSMODE_ATTRACT,
        LIGHTSMODE_JOINING,
        LIGHTSMODE_MENU_START_ONLY,
        LIGHTSMODE_MENU_START_AND_DIRECTIONS,
        LIGHTSMODE_DEMONSTRATION,
        LIGHTSMODE_GAMEPLAY,
        LIGHTSMODE_STAGE,
        LIGHTSMODE_ALL_CLEARED,
        LIGHTSMODE_TEST_AUTO_CYCLE,
        LIGHTSMODE_TEST_MANUAL_CYCLE,
        NUM_LightsMode,
        LightsMode_Invalid
    };

    [Flags]
    public enum CabinetLight
    {
        NONE = 0,
        [System.ComponentModel.Description("Marquee Up Left")]
        LIGHT_MARQUEE_UP_LEFT = 1,
        [System.ComponentModel.Description("Marquee Up Right")]
        LIGHT_MARQUEE_UP_RIGHT = 2,
        [System.ComponentModel.Description("Marquee Lower Left")]
        LIGHT_MARQUEE_LR_LEFT = 4,
        [System.ComponentModel.Description("Marquee Lower Right")]
        LIGHT_MARQUEE_LR_RIGHT = 8,
        [System.ComponentModel.Description("Bass Left")]
        LIGHT_BASS_LEFT = 16,
        [System.ComponentModel.Description("Bass Right")]
        LIGHT_BASS_RIGHT = 32,
        NUM_CabinetLight = 64,
        CabinetLight_Invalid = 128,
    };

    [Flags]
    public enum GameButton
    {
        NONE = 0,
        [System.ComponentModel.Description("Menu Left")]
        GAME_BUTTON_MENULEFT = 1,
        [System.ComponentModel.Description("Menu Right")]
        GAME_BUTTON_MENURIGHT = 2,
        [System.ComponentModel.Description("Menu Up")]
        GAME_BUTTON_MENUUP = 4,
        [System.ComponentModel.Description("Menu Down")]
        GAME_BUTTON_MENUDOWN = 8,
        [System.ComponentModel.Description("Start")]
        GAME_BUTTON_START = 16,
        [System.ComponentModel.Description("Select")]
        GAME_BUTTON_SELECT = 32,
        [System.ComponentModel.Description("Back")]
        GAME_BUTTON_BACK = 64,
        [System.ComponentModel.Description("Restart")]
        GAME_BUTTON_RESTART = 128,
        [System.ComponentModel.Description("Coin")]
        GAME_BUTTON_COIN = 256,
        [System.ComponentModel.Description("Operator")]
        GAME_BUTTON_OPERATOR = 512,
        [System.ComponentModel.Description("Effect Up")]
        GAME_BUTTON_EFFECT_UP = 1024,
        [System.ComponentModel.Description("Effect Down")]
        GAME_BUTTON_EFFECT_DOWN = 2048,
        [System.ComponentModel.Description("P1 Pad Left")]
        GAME_BUTTON_CUSTOM_01 = 4096,
        [System.ComponentModel.Description("P1 Pad Right")]
        GAME_BUTTON_CUSTOM_02 = 8192,
        [System.ComponentModel.Description("P1 Pad Up")]
        GAME_BUTTON_CUSTOM_03 = 16384,
        [System.ComponentModel.Description("P1 Pad Down")]
        GAME_BUTTON_CUSTOM_04 = 32768,
        [System.ComponentModel.Description("P1 Pad Up-Left (Solo)")]
        GAME_BUTTON_CUSTOM_05 = 65536,
        [System.ComponentModel.Description("P1 Pad Up-Right (Solo)")]
        GAME_BUTTON_CUSTOM_06 = 131072,
        [System.ComponentModel.Description("P1 Custom 07")]
        GAME_BUTTON_CUSTOM_07 = 262144,
        [System.ComponentModel.Description("P1 Custom 08")]
        GAME_BUTTON_CUSTOM_08 = 524288,
        [System.ComponentModel.Description("P1 Custom 09")]
        GAME_BUTTON_CUSTOM_09 = 1048576,
        [System.ComponentModel.Description("P2 Pad Left")]
        GAME_BUTTON_CUSTOM_10 = 2097152,
        [System.ComponentModel.Description("P2 Pad Right")]
        GAME_BUTTON_CUSTOM_11 = 4194304,
        [System.ComponentModel.Description("P2 Pad Up")]
        GAME_BUTTON_CUSTOM_12 = 8388608,
        [System.ComponentModel.Description("P2 Pad Down")]
        GAME_BUTTON_CUSTOM_13 = 16777216,
        [System.ComponentModel.Description("P2 Pad Up-Left (Solo)")]
        GAME_BUTTON_CUSTOM_14 = 33554432,
        [System.ComponentModel.Description("P2 Pad Up-Right (Solo)")]
        GAME_BUTTON_CUSTOM_15 = 67108864,
        [System.ComponentModel.Description("P2 Custom 07")]
        GAME_BUTTON_CUSTOM_16 = 134217728,
        [System.ComponentModel.Description("P2 Custom 08")]
        GAME_BUTTON_CUSTOM_17 = 268435456,
        [System.ComponentModel.Description("P2 Custom 09")]
        GAME_BUTTON_CUSTOM_18 = 536870912,
        [System.ComponentModel.Description("P2 Custom 10")]
        GAME_BUTTON_CUSTOM_19 = 1073741824,
    };

}
