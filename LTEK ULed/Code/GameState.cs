using Avalonia.Threading;
using LTEK_ULed.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LTEK_ULed.Code
{
    internal class GameState
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


    internal class State
    {
        public LightsMode lightsMode;

        public CabinetLight cabinetLight;

        public GameButton gameButton;

        public uint p1Combo;
        public float p1Precision;

        public uint p2Combo;
        public float p2Precision;
    }

    enum LightsMode
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
    enum CabinetLight
    {
        NONE = 0,
        LIGHT_MARQUEE_UP_LEFT = 1,
        LIGHT_MARQUEE_UP_RIGHT = 2,
        LIGHT_MARQUEE_LR_LEFT = 4,
        LIGHT_MARQUEE_LR_RIGHT = 8,
        LIGHT_BASS_LEFT = 16,
        LIGHT_BASS_RIGHT = 32,
        NUM_CabinetLight = 64,
        CabinetLight_Invalid = 128,
    };

    [Flags]
    enum GameButton
    {
        GAME_BUTTON_MENULEFT = 1, /**< Navigate the menus to the left. */
        GAME_BUTTON_MENURIGHT = 2, /**< Navigate the menus to the right. */
        GAME_BUTTON_MENUUP = 4, /**< Navigate the menus to the top. */
        GAME_BUTTON_MENUDOWN = 8, /**< Navigate the menus to the bottom. */
        GAME_BUTTON_START = 16,
        GAME_BUTTON_SELECT = 32,
        GAME_BUTTON_BACK = 64,
        GAME_BUTTON_RESTART = 128,
        GAME_BUTTON_COIN = 256, /**< Insert a coin to play. */
        GAME_BUTTON_OPERATOR = 512, /**< Access the operator menu. */
        GAME_BUTTON_EFFECT_UP = 1024,
        GAME_BUTTON_EFFECT_DOWN = 2048,
        GAME_BUTTON_CUSTOM_01 = 4096,
        GAME_BUTTON_CUSTOM_02 = 8192,
        GAME_BUTTON_CUSTOM_03 = 16384,
        GAME_BUTTON_CUSTOM_04 = 32768,
        GAME_BUTTON_CUSTOM_05 = 65536,
        GAME_BUTTON_CUSTOM_06 = 131072,
        GAME_BUTTON_CUSTOM_07 = 262144,
        GAME_BUTTON_CUSTOM_08 = 524288,
        GAME_BUTTON_CUSTOM_09 = 1048576,
        GAME_BUTTON_CUSTOM_10 = 2097152,
        GAME_BUTTON_CUSTOM_11 = 4194304,
        GAME_BUTTON_CUSTOM_12 = 8388608,
        GAME_BUTTON_CUSTOM_13 = 16777216,
        GAME_BUTTON_CUSTOM_14 = 33554432,
        GAME_BUTTON_CUSTOM_15 = 67108864,
        GAME_BUTTON_CUSTOM_16 = 134217728,
        GAME_BUTTON_CUSTOM_17 = 268435456,
        GAME_BUTTON_CUSTOM_18 = 536870912,
        GAME_BUTTON_CUSTOM_19 = 1073741824,
    };

}
