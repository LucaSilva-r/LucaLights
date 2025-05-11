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

        public void Parse(byte[] data)
        {

            CabinetLight cabinetLight = (CabinetLight)(data[0] & 0b0011111); 

            //GameButton buttons = (GameButton)BitConverter.ToUInt16(data, new byte[] {});
            LightsMode lightsMode = (LightsMode)data[0];

            lock (state)
            {
                state.cabinetLight = cabinetLight;
                state.lightsMode = lightsMode;
            }
        }
    }


    internal class State
    {
        public LightsMode lightsMode;

        public CabinetLight cabinetLight;

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
        LIGHT_MARQUEE_UP_LEFT=1,
        LIGHT_MARQUEE_UP_RIGHT=2,
        LIGHT_MARQUEE_LR_LEFT=4,
        LIGHT_MARQUEE_LR_RIGHT=8,
        LIGHT_BASS_LEFT=16,
        LIGHT_BASS_RIGHT=32,
        NUM_CabinetLight=64,
        CabinetLight_Invalid=128,
    };

    /** @brief the list of buttons StepMania recognizes. */
    enum GameButton
    {
        GAME_BUTTON_MENULEFT, /**< Navigate the menus to the left. */
        GAME_BUTTON_MENURIGHT, /**< Navigate the menus to the right. */
        GAME_BUTTON_MENUUP, /**< Navigate the menus to the top. */
        GAME_BUTTON_MENUDOWN, /**< Navigate the menus to the bottom. */
        GAME_BUTTON_START,
        GAME_BUTTON_SELECT,
        GAME_BUTTON_BACK,
        GAME_BUTTON_RESTART,
        GAME_BUTTON_COIN, /**< Insert a coin to play. */
        GAME_BUTTON_OPERATOR, /**< Access the operator menu. */
        GAME_BUTTON_EFFECT_UP,
        GAME_BUTTON_EFFECT_DOWN,
        GAME_BUTTON_CUSTOM_01,
        GAME_BUTTON_CUSTOM_02,
        GAME_BUTTON_CUSTOM_03,
        GAME_BUTTON_CUSTOM_04,
        GAME_BUTTON_CUSTOM_05,
        GAME_BUTTON_CUSTOM_06,
        GAME_BUTTON_CUSTOM_07,
        GAME_BUTTON_CUSTOM_08,
        GAME_BUTTON_CUSTOM_09,
        GAME_BUTTON_CUSTOM_10,
        GAME_BUTTON_CUSTOM_11,
        GAME_BUTTON_CUSTOM_12,
        GAME_BUTTON_CUSTOM_13,
        GAME_BUTTON_CUSTOM_14,
        GAME_BUTTON_CUSTOM_15,
        GAME_BUTTON_CUSTOM_16,
        GAME_BUTTON_CUSTOM_17,
        GAME_BUTTON_CUSTOM_18,
        GAME_BUTTON_CUSTOM_19,

        NUM_GameButton,
        GameButton_Invalid
    };

}
