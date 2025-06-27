using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LTEK_ULed.Code
{
    public class GradientPreset
    {
        public string name
        {
            get;
        }
        public LinearGradientBrush gradientBrush
        {
            get;
        }
        public GradientPreset(string name, LinearGradientBrush gradientBrush)
        {
            this.name = name;
            this.gradientBrush = gradientBrush;
        }
        public static List<GradientPreset> GetPresets()
        {
            return new List<GradientPreset> {
                new GradientPreset("Analogous_1", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(51, 0, 255), 0.0),
                      new GradientStop(Color.FromRgb(102, 0, 255), 0.25),
                      new GradientStop(Color.FromRgb(153, 0, 255), 0.5),
                      new GradientStop(Color.FromRgb(204, 0, 128), 0.75),
                      new GradientStop(Color.FromRgb(255, 0, 0), 1.0)
                    }
                }),
                new GradientPreset("Another_Sunset", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(185, 121, 73), 0.0),
                      new GradientStop(Color.FromRgb(142, 103, 71), 0.1173),
                      new GradientStop(Color.FromRgb(100, 84, 69), 0.2676),
                      new GradientStop(Color.FromRgb(249, 184, 66), 0.2676),
                      new GradientStop(Color.FromRgb(241, 204, 105), 0.3823),
                      new GradientStop(Color.FromRgb(234, 225, 144), 0.48840000000000006),
                      new GradientStop(Color.FromRgb(117, 125, 140), 0.7012),
                      new GradientStop(Color.FromRgb(0, 26, 136), 1.0)
                    }
                }),
                new GradientPreset("Beech", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(255, 254, 238), 0.0),
                      new GradientStop(Color.FromRgb(255, 254, 238), 0.0484),
                      new GradientStop(Color.FromRgb(255, 254, 238), 0.0901),
                      new GradientStop(Color.FromRgb(228, 224, 186), 0.1035),
                      new GradientStop(Color.FromRgb(201, 195, 135), 0.11349999999999999),
                      new GradientStop(Color.FromRgb(186, 255, 234), 0.11349999999999999),
                      new GradientStop(Color.FromRgb(138, 251, 238), 0.19870000000000002),
                      new GradientStop(Color.FromRgb(90, 246, 243), 0.2788),
                      new GradientStop(Color.FromRgb(45, 225, 231), 0.36560000000000004),
                      new GradientStop(Color.FromRgb(0, 204, 219), 0.4725),
                      new GradientStop(Color.FromRgb(8, 168, 186), 0.5242),
                      new GradientStop(Color.FromRgb(16, 132, 153), 0.5342),
                      new GradientStop(Color.FromRgb(65, 189, 217), 0.5342),
                      new GradientStop(Color.FromRgb(33, 159, 207), 0.8184),
                      new GradientStop(Color.FromRgb(0, 129, 197), 1.0)
                    }
                }),
                new GradientPreset("BlacK_Blue_Magenta_White", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(0, 0, 0), 0.0),
                      new GradientStop(Color.FromRgb(0, 0, 128), 0.16670000000000001),
                      new GradientStop(Color.FromRgb(0, 0, 255), 0.3333),
                      new GradientStop(Color.FromRgb(128, 0, 255), 0.5),
                      new GradientStop(Color.FromRgb(255, 0, 255), 0.6667000000000001),
                      new GradientStop(Color.FromRgb(255, 128, 255), 0.8332999999999999),
                      new GradientStop(Color.FromRgb(255, 255, 255), 1.0)
                    }
                }),
                new GradientPreset("BlacK_Magenta_Red", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(0, 0, 0), 0.0),
                      new GradientStop(Color.FromRgb(128, 0, 128), 0.25),
                      new GradientStop(Color.FromRgb(255, 0, 255), 0.5),
                      new GradientStop(Color.FromRgb(255, 0, 128), 0.75),
                      new GradientStop(Color.FromRgb(255, 0, 0), 1.0)
                    }
                }),
                new GradientPreset("BlacK_Red_Magenta_Yellow", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(0, 0, 0), 0.0),
                      new GradientStop(Color.FromRgb(128, 0, 0), 0.16670000000000001),
                      new GradientStop(Color.FromRgb(255, 0, 0), 0.3333),
                      new GradientStop(Color.FromRgb(255, 0, 128), 0.5),
                      new GradientStop(Color.FromRgb(255, 0, 255), 0.6667000000000001),
                      new GradientStop(Color.FromRgb(255, 128, 128), 0.8332999999999999),
                      new GradientStop(Color.FromRgb(255, 255, 0), 1.0)
                    }
                }),
                new GradientPreset("Blue_Cyan_Yellow", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(0, 0, 255), 0.0),
                      new GradientStop(Color.FromRgb(0, 128, 255), 0.25),
                      new GradientStop(Color.FromRgb(0, 255, 255), 0.5),
                      new GradientStop(Color.FromRgb(128, 255, 128), 0.75),
                      new GradientStop(Color.FromRgb(255, 255, 0), 1.0)
                    }
                }),
                new GradientPreset("Colorfull", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(76, 155, 54), 0.0),
                      new GradientStop(Color.FromRgb(111, 174, 89), 0.1),
                      new GradientStop(Color.FromRgb(146, 193, 125), 0.2354),
                      new GradientStop(Color.FromRgb(166, 166, 136), 0.36560000000000004),
                      new GradientStop(Color.FromRgb(185, 138, 147), 0.4157),
                      new GradientStop(Color.FromRgb(193, 121, 148), 0.429),
                      new GradientStop(Color.FromRgb(202, 104, 149), 0.44409999999999994),
                      new GradientStop(Color.FromRgb(229, 179, 174), 0.45740000000000003),
                      new GradientStop(Color.FromRgb(255, 255, 199), 0.4891),
                      new GradientStop(Color.FromRgb(178, 218, 209), 0.6594),
                      new GradientStop(Color.FromRgb(100, 182, 219), 1.0)
                    }
                }),
                new GradientPreset("GMT_drywet", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(134, 97, 42), 0.0),
                      new GradientStop(Color.FromRgb(238, 199, 100), 0.16670000000000001),
                      new GradientStop(Color.FromRgb(180, 238, 135), 0.3333),
                      new GradientStop(Color.FromRgb(50, 238, 235), 0.5),
                      new GradientStop(Color.FromRgb(12, 120, 238), 0.6667000000000001),
                      new GradientStop(Color.FromRgb(38, 1, 183), 0.8332999999999999),
                      new GradientStop(Color.FromRgb(8, 51, 113), 1.0)
                    }
                }),
                new GradientPreset("Pink_Purple", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(95, 32, 121), 0.0),
                      new GradientStop(Color.FromRgb(106, 40, 128), 0.1),
                      new GradientStop(Color.FromRgb(117, 48, 135), 0.2),
                      new GradientStop(Color.FromRgb(154, 135, 192), 0.3),
                      new GradientStop(Color.FromRgb(190, 222, 249), 0.4),
                      new GradientStop(Color.FromRgb(215, 236, 252), 0.429),
                      new GradientStop(Color.FromRgb(240, 250, 255), 0.44909999999999994),
                      new GradientStop(Color.FromRgb(213, 200, 241), 0.47909999999999997),
                      new GradientStop(Color.FromRgb(187, 149, 226), 0.5876),
                      new GradientStop(Color.FromRgb(196, 130, 209), 0.7195),
                      new GradientStop(Color.FromRgb(206, 111, 191), 1.0)
                    }
                }),
                new GradientPreset("Sunset_Real", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(191, 0, 0), 0.0),
                      new GradientStop(Color.FromRgb(223, 85, 0), 0.0885),
                      new GradientStop(Color.FromRgb(255, 170, 0), 0.20370000000000002),
                      new GradientStop(Color.FromRgb(217, 85, 89), 0.33390000000000003),
                      new GradientStop(Color.FromRgb(178, 0, 178), 0.5326),
                      new GradientStop(Color.FromRgb(89, 0, 195), 0.7777),
                      new GradientStop(Color.FromRgb(0, 0, 212), 1.0)
                    }
                }),
                new GradientPreset("Sunset_Yellow", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(76, 135, 191), 0.0),
                      new GradientStop(Color.FromRgb(143, 188, 178), 0.145),
                      new GradientStop(Color.FromRgb(210, 241, 165), 0.3422),
                      new GradientStop(Color.FromRgb(232, 237, 151), 0.3923),
                      new GradientStop(Color.FromRgb(255, 232, 138), 0.4224),
                      new GradientStop(Color.FromRgb(252, 202, 141), 0.45409999999999995),
                      new GradientStop(Color.FromRgb(249, 172, 144), 0.4725),
                      new GradientStop(Color.FromRgb(252, 202, 141), 0.5042),
                      new GradientStop(Color.FromRgb(255, 232, 138), 0.7095),
                      new GradientStop(Color.FromRgb(255, 242, 131), 0.875),
                      new GradientStop(Color.FromRgb(255, 252, 125), 1.0)
                    }
                }),
                new GradientPreset("Tertiary_01", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(0, 25, 255), 0.0),
                      new GradientStop(Color.FromRgb(51, 140, 128), 0.25),
                      new GradientStop(Color.FromRgb(102, 255, 0), 0.5),
                      new GradientStop(Color.FromRgb(178, 140, 26), 0.75),
                      new GradientStop(Color.FromRgb(255, 25, 51), 1.0)
                    }
                }),
                new GradientPreset("bhw1_01", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(244, 168, 48), 0.0),
                      new GradientStop(Color.FromRgb(230, 78, 92), 0.46),
                      new GradientStop(Color.FromRgb(173, 54, 228), 1.0)
                    }
                }),
                new GradientPreset("bhw1_04", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(245, 242, 31), 0.0),
                      new GradientStop(Color.FromRgb(244, 168, 48), 0.0601),
                      new GradientStop(Color.FromRgb(126, 21, 161), 0.5600999999999999),
                      new GradientStop(Color.FromRgb(90, 22, 160), 0.78),
                      new GradientStop(Color.FromRgb(0, 0, 128), 1.0)
                    }
                }),
                new GradientPreset("bhw1_05", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(5, 239, 137), 0.0),
                      new GradientStop(Color.FromRgb(158, 35, 221), 1.0)
                    }
                }),
                new GradientPreset("bhw1_06", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(225, 19, 194), 0.0),
                      new GradientStop(Color.FromRgb(19, 225, 223), 0.6299),
                      new GradientStop(Color.FromRgb(210, 242, 227), 0.8601000000000001),
                      new GradientStop(Color.FromRgb(255, 255, 255), 1.0)
                    }
                }),
                new GradientPreset("bhw1_14", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(0, 0, 0), 0.0),
                      new GradientStop(Color.FromRgb(35, 4, 48), 0.0483),
                      new GradientStop(Color.FromRgb(70, 8, 96), 0.21),
                      new GradientStop(Color.FromRgb(56, 48, 168), 0.3166),
                      new GradientStop(Color.FromRgb(43, 89, 239), 0.47),
                      new GradientStop(Color.FromRgb(64, 59, 175), 0.5714),
                      new GradientStop(Color.FromRgb(86, 30, 110), 0.73),
                      new GradientStop(Color.FromRgb(43, 15, 55), 0.9163),
                      new GradientStop(Color.FromRgb(0, 0, 0), 1.0)
                    }
                }),
                new GradientPreset("bhw1_three", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(255, 255, 255), 0.0),
                      new GradientStop(Color.FromRgb(64, 64, 255), 0.17989999999999998),
                      new GradientStop(Color.FromRgb(244, 16, 193), 0.4399),
                      new GradientStop(Color.FromRgb(244, 16, 193), 0.4399),
                      new GradientStop(Color.FromRgb(255, 255, 255), 0.55),
                      new GradientStop(Color.FromRgb(244, 16, 193), 0.6101),
                      new GradientStop(Color.FromRgb(131, 13, 175), 0.77),
                      new GradientStop(Color.FromRgb(255, 255, 255), 1.0)
                    }
                }),
                new GradientPreset("bhw1_w00t", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(49, 68, 126), 0.0),
                      new GradientStop(Color.FromRgb(162, 195, 249), 0.40990000000000004),
                      new GradientStop(Color.FromRgb(255, 0, 0), 0.74),
                      new GradientStop(Color.FromRgb(110, 14, 14), 1.0)
                    }
                }),
                new GradientPreset("bhw2_22", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(0, 0, 0), 0.0),
                      new GradientStop(Color.FromRgb(244, 12, 12), 0.3899),
                      new GradientStop(Color.FromRgb(253, 228, 172), 0.51),
                      new GradientStop(Color.FromRgb(244, 12, 12), 0.6101),
                      new GradientStop(Color.FromRgb(0, 0, 0), 1.0)
                    }
                }),
                new GradientPreset("bhw2_23", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(0, 0, 0), 0.0),
                      new GradientStop(Color.FromRgb(144, 242, 246), 0.26),
                      new GradientStop(Color.FromRgb(255, 255, 64), 0.3799),
                      new GradientStop(Color.FromRgb(255, 255, 255), 0.49),
                      new GradientStop(Color.FromRgb(255, 255, 64), 0.6001),
                      new GradientStop(Color.FromRgb(144, 242, 246), 0.74),
                      new GradientStop(Color.FromRgb(0, 0, 0), 1.0)
                    }
                }),
                new GradientPreset("bhw2_45", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(0, 0, 0), 0.0),
                      new GradientStop(Color.FromRgb(30, 21, 30), 0.0384),
                      new GradientStop(Color.FromRgb(60, 43, 60), 0.15990000000000001),
                      new GradientStop(Color.FromRgb(60, 43, 60), 0.26),
                      new GradientStop(Color.FromRgb(76, 16, 77), 0.39990000000000003),
                      new GradientStop(Color.FromRgb(0, 0, 0), 1.0)
                    }
                }),
                new GradientPreset("bhw2_xc", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(56, 30, 68), 0.0),
                      new GradientStop(Color.FromRgb(89, 0, 130), 0.23),
                      new GradientStop(Color.FromRgb(103, 0, 86), 0.48),
                      new GradientStop(Color.FromRgb(205, 57, 29), 0.6201),
                      new GradientStop(Color.FromRgb(223, 117, 35), 0.72),
                      new GradientStop(Color.FromRgb(241, 177, 41), 0.8601000000000001),
                      new GradientStop(Color.FromRgb(247, 247, 35), 1.0)
                    }
                }),
                new GradientPreset("bhw3_40", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(7, 7, 7), 0.0),
                      new GradientStop(Color.FromRgb(53, 25, 73), 0.1699),
                      new GradientStop(Color.FromRgb(76, 15, 46), 0.3),
                      new GradientStop(Color.FromRgb(214, 39, 108), 0.4299),
                      new GradientStop(Color.FromRgb(255, 156, 191), 0.5),
                      new GradientStop(Color.FromRgb(194, 73, 212), 0.6498999999999999),
                      new GradientStop(Color.FromRgb(120, 66, 242), 0.8),
                      new GradientStop(Color.FromRgb(93, 29, 90), 1.0)
                    }
                }),
                new GradientPreset("bhw3_52", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(114, 22, 105), 0.0),
                      new GradientStop(Color.FromRgb(118, 22, 85), 0.17989999999999998),
                      new GradientStop(Color.FromRgb(201, 45, 67), 0.3899),
                      new GradientStop(Color.FromRgb(238, 187, 70), 0.52),
                      new GradientStop(Color.FromRgb(232, 85, 34), 0.6899),
                      new GradientStop(Color.FromRgb(232, 56, 59), 0.79),
                      new GradientStop(Color.FromRgb(5, 0, 4), 1.0)
                    }
                }),
                new GradientPreset("bhw4_017", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(124, 102, 114), 0.0),
                      new GradientStop(Color.FromRgb(55, 49, 83), 0.1001),
                      new GradientStop(Color.FromRgb(136, 96, 96), 0.18989999999999999),
                      new GradientStop(Color.FromRgb(243, 214, 34), 0.29),
                      new GradientStop(Color.FromRgb(222, 104, 54), 0.35009999999999997),
                      new GradientStop(Color.FromRgb(55, 49, 83), 0.51),
                      new GradientStop(Color.FromRgb(255, 177, 58), 0.6399),
                      new GradientStop(Color.FromRgb(243, 214, 34), 0.73),
                      new GradientStop(Color.FromRgb(124, 102, 114), 0.8301000000000001),
                      new GradientStop(Color.FromRgb(29, 19, 18), 1.0)
                    }
                }),
                new GradientPreset("bhw4_097", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(252, 46, 0), 0.0),
                      new GradientStop(Color.FromRgb(255, 139, 33), 0.1101),
                      new GradientStop(Color.FromRgb(247, 158, 74), 0.1699),
                      new GradientStop(Color.FromRgb(247, 216, 134), 0.23),
                      new GradientStop(Color.FromRgb(245, 94, 15), 0.3301),
                      new GradientStop(Color.FromRgb(187, 65, 16), 0.45),
                      new GradientStop(Color.FromRgb(255, 241, 127), 0.55),
                      new GradientStop(Color.FromRgb(187, 65, 16), 0.6598999999999999),
                      new GradientStop(Color.FromRgb(251, 233, 167), 0.77),
                      new GradientStop(Color.FromRgb(255, 94, 9), 0.8501000000000001),
                      new GradientStop(Color.FromRgb(140, 8, 6), 1.0)
                    }
                }),
                new GradientPreset("departure", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(68, 34, 0), 0.0),
                      new GradientStop(Color.FromRgb(68, 34, 0), 0.16670000000000001),
                      new GradientStop(Color.FromRgb(102, 51, 0), 0.16670000000000001),
                      new GradientStop(Color.FromRgb(102, 51, 0), 0.25),
                      new GradientStop(Color.FromRgb(160, 108, 60), 0.25),
                      new GradientStop(Color.FromRgb(160, 108, 60), 0.3333),
                      new GradientStop(Color.FromRgb(218, 166, 120), 0.3333),
                      new GradientStop(Color.FromRgb(218, 166, 120), 0.4167),
                      new GradientStop(Color.FromRgb(238, 212, 188), 0.4167),
                      new GradientStop(Color.FromRgb(238, 212, 188), 0.4583),
                      new GradientStop(Color.FromRgb(255, 255, 255), 0.4583),
                      new GradientStop(Color.FromRgb(255, 255, 255), 0.5417000000000001),
                      new GradientStop(Color.FromRgb(200, 255, 200), 0.5417000000000001),
                      new GradientStop(Color.FromRgb(200, 255, 200), 0.5832999999999999),
                      new GradientStop(Color.FromRgb(100, 255, 100), 0.5832999999999999),
                      new GradientStop(Color.FromRgb(100, 255, 100), 0.6667000000000001),
                      new GradientStop(Color.FromRgb(0, 255, 0), 0.6667000000000001),
                      new GradientStop(Color.FromRgb(0, 255, 0), 0.75),
                      new GradientStop(Color.FromRgb(0, 192, 0), 0.75),
                      new GradientStop(Color.FromRgb(0, 192, 0), 0.8332999999999999),
                      new GradientStop(Color.FromRgb(0, 128, 0), 0.8332999999999999),
                      new GradientStop(Color.FromRgb(0, 128, 0), 1.0)
                    }
                }),
                // Error: No valid color stops found in preset 'downloadUrls'.,
                new GradientPreset("es_autumn_19", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(106, 14, 8), 0.0),
                      new GradientStop(Color.FromRgb(153, 41, 19), 0.2),
                      new GradientStop(Color.FromRgb(190, 70, 24), 0.3301),
                      new GradientStop(Color.FromRgb(201, 202, 136), 0.40990000000000004),
                      new GradientStop(Color.FromRgb(187, 137, 5), 0.4399),
                      new GradientStop(Color.FromRgb(199, 200, 142), 0.48),
                      new GradientStop(Color.FromRgb(201, 202, 135), 0.49),
                      new GradientStop(Color.FromRgb(187, 137, 5), 0.53),
                      new GradientStop(Color.FromRgb(202, 203, 129), 0.5600999999999999),
                      new GradientStop(Color.FromRgb(187, 68, 24), 0.6399),
                      new GradientStop(Color.FromRgb(142, 35, 17), 0.8),
                      new GradientStop(Color.FromRgb(90, 5, 4), 0.98),
                      new GradientStop(Color.FromRgb(90, 5, 4), 1.0)
                    }
                }),
                new GradientPreset("es_landscape_33", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(19, 45, 0), 0.0),
                      new GradientStop(Color.FromRgb(116, 86, 3), 0.0779),
                      new GradientStop(Color.FromRgb(214, 128, 7), 0.1499),
                      new GradientStop(Color.FromRgb(245, 197, 25), 0.25),
                      new GradientStop(Color.FromRgb(124, 196, 156), 0.26),
                      new GradientStop(Color.FromRgb(9, 39, 11), 1.0)
                    }
                }),
                new GradientPreset("es_landscape_64", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(0, 0, 0), 0.0),
                      new GradientStop(Color.FromRgb(43, 89, 26), 0.147),
                      new GradientStop(Color.FromRgb(87, 178, 53), 0.3),
                      new GradientStop(Color.FromRgb(163, 235, 8), 0.5),
                      new GradientStop(Color.FromRgb(195, 234, 130), 0.5052),
                      new GradientStop(Color.FromRgb(227, 233, 252), 0.51),
                      new GradientStop(Color.FromRgb(205, 219, 234), 0.6001),
                      new GradientStop(Color.FromRgb(146, 179, 253), 0.8),
                      new GradientStop(Color.FromRgb(39, 107, 228), 1.0)
                    }
                }),
                new GradientPreset("es_ocean_breeze_036", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(25, 48, 62), 0.0),
                      new GradientStop(Color.FromRgb(38, 166, 183), 0.35009999999999997),
                      new GradientStop(Color.FromRgb(205, 233, 255), 0.6001),
                      new GradientStop(Color.FromRgb(0, 145, 162), 1.0)
                    }
                }),
                new GradientPreset("es_pinksplash_08", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(195, 63, 255), 0.0),
                      new GradientStop(Color.FromRgb(231, 9, 97), 0.5),
                      new GradientStop(Color.FromRgb(237, 205, 218), 0.6899),
                      new GradientStop(Color.FromRgb(212, 38, 184), 0.8701000000000001),
                      new GradientStop(Color.FromRgb(212, 38, 184), 1.0)
                    }
                }),
                new GradientPreset("es_rivendell_15", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(35, 69, 54), 0.0),
                      new GradientStop(Color.FromRgb(88, 105, 82), 0.39990000000000003),
                      new GradientStop(Color.FromRgb(143, 140, 109), 0.6498999999999999),
                      new GradientStop(Color.FromRgb(208, 204, 175), 0.95),
                      new GradientStop(Color.FromRgb(208, 204, 175), 1.0)
                    }
                }),
                new GradientPreset("es_vintage_01", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(54, 18, 32), 0.0),
                      new GradientStop(Color.FromRgb(89, 0, 30), 0.2),
                      new GradientStop(Color.FromRgb(176, 170, 48), 0.3),
                      new GradientStop(Color.FromRgb(255, 189, 92), 0.39990000000000003),
                      new GradientStop(Color.FromRgb(153, 56, 50), 0.5),
                      new GradientStop(Color.FromRgb(89, 0, 30), 0.6001),
                      new GradientStop(Color.FromRgb(54, 18, 32), 0.8998999999999999),
                      new GradientStop(Color.FromRgb(54, 18, 32), 1.0)
                    }
                }),
                new GradientPreset("es_vintage_57", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(41, 8, 5), 0.0),
                      new GradientStop(Color.FromRgb(92, 1, 0), 0.21),
                      new GradientStop(Color.FromRgb(155, 96, 36), 0.4089),
                      new GradientStop(Color.FromRgb(217, 191, 72), 0.6001),
                      new GradientStop(Color.FromRgb(132, 129, 52), 1.0)
                    }
                }),
                new GradientPreset("fierce-ice", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(0, 0, 0), 0.0),
                      new GradientStop(Color.FromRgb(0, 51, 128), 0.2339),
                      new GradientStop(Color.FromRgb(0, 102, 255), 0.4678),
                      new GradientStop(Color.FromRgb(51, 153, 255), 0.5877),
                      new GradientStop(Color.FromRgb(102, 204, 255), 0.7076),
                      new GradientStop(Color.FromRgb(178, 230, 255), 0.8538),
                      new GradientStop(Color.FromRgb(255, 255, 255), 1.0)
                    }
                }),
                new GradientPreset("gr64_hult", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(34, 184, 182), 0.0),
                      new GradientStop(Color.FromRgb(14, 162, 160), 0.26),
                      new GradientStop(Color.FromRgb(139, 137, 11), 0.40990000000000004),
                      new GradientStop(Color.FromRgb(188, 186, 30), 0.51),
                      new GradientStop(Color.FromRgb(139, 137, 11), 0.5901),
                      new GradientStop(Color.FromRgb(10, 156, 154), 0.79),
                      new GradientStop(Color.FromRgb(0, 128, 128), 0.9399),
                      new GradientStop(Color.FromRgb(0, 128, 128), 1.0)
                    }
                }),
                new GradientPreset("gr65_hult", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(252, 216, 252), 0.0),
                      new GradientStop(Color.FromRgb(255, 192, 255), 0.18989999999999999),
                      new GradientStop(Color.FromRgb(241, 95, 243), 0.35009999999999997),
                      new GradientStop(Color.FromRgb(65, 153, 221), 0.6299),
                      new GradientStop(Color.FromRgb(34, 184, 182), 0.8501000000000001),
                      new GradientStop(Color.FromRgb(34, 184, 182), 1.0)
                    }
                }),
                new GradientPreset("ib15", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(187, 160, 205), 0.0),
                      new GradientStop(Color.FromRgb(212, 158, 159), 0.2836),
                      new GradientStop(Color.FromRgb(236, 155, 113), 0.35009999999999997),
                      new GradientStop(Color.FromRgb(255, 95, 74), 0.4199),
                      new GradientStop(Color.FromRgb(201, 98, 121), 0.5533),
                      new GradientStop(Color.FromRgb(146, 101, 168), 1.0)
                    }
                }),
                new GradientPreset("ib_jul01", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(230, 6, 17), 0.0),
                      new GradientStop(Color.FromRgb(37, 96, 90), 0.3701),
                      new GradientStop(Color.FromRgb(144, 189, 106), 0.52),
                      new GradientStop(Color.FromRgb(187, 3, 13), 1.0)
                    }
                }),
                new GradientPreset("lava", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(0, 0, 0), 0.0),
                      new GradientStop(Color.FromRgb(93, 0, 0), 0.1812),
                      new GradientStop(Color.FromRgb(187, 0, 0), 0.3801),
                      new GradientStop(Color.FromRgb(204, 38, 13), 0.424),
                      new GradientStop(Color.FromRgb(221, 76, 26), 0.4678),
                      new GradientStop(Color.FromRgb(238, 115, 38), 0.5760000000000001),
                      new GradientStop(Color.FromRgb(255, 153, 51), 0.6842),
                      new GradientStop(Color.FromRgb(255, 178, 51), 0.7398),
                      new GradientStop(Color.FromRgb(255, 204, 51), 0.7953),
                      new GradientStop(Color.FromRgb(255, 230, 51), 0.8567),
                      new GradientStop(Color.FromRgb(255, 255, 51), 0.9181),
                      new GradientStop(Color.FromRgb(255, 255, 153), 0.9591),
                      new GradientStop(Color.FromRgb(255, 255, 255), 1.0)
                    }
                }),
                new GradientPreset("rainbowsherbet", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(255, 102, 51), 0.0),
                      new GradientStop(Color.FromRgb(255, 140, 102), 0.1699),
                      new GradientStop(Color.FromRgb(255, 51, 102), 0.34009999999999996),
                      new GradientStop(Color.FromRgb(255, 153, 178), 0.5),
                      new GradientStop(Color.FromRgb(255, 255, 250), 0.6698999999999999),
                      new GradientStop(Color.FromRgb(128, 255, 97), 0.8201),
                      new GradientStop(Color.FromRgb(169, 255, 148), 1.0)
                    }
                }),
                new GradientPreset("retro2_16", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(227, 191, 12), 0.0),
                      new GradientStop(Color.FromRgb(132, 52, 2), 1.0)
                    }
                }),
                new GradientPreset("rgi_15", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(54, 14, 111), 0.0),
                      new GradientStop(Color.FromRgb(142, 24, 86), 0.125),
                      new GradientStop(Color.FromRgb(231, 34, 61), 0.25),
                      new GradientStop(Color.FromRgb(146, 31, 88), 0.375),
                      new GradientStop(Color.FromRgb(61, 29, 114), 0.5),
                      new GradientStop(Color.FromRgb(124, 47, 113), 0.625),
                      new GradientStop(Color.FromRgb(186, 66, 112), 0.75),
                      new GradientStop(Color.FromRgb(143, 57, 116), 0.875),
                      new GradientStop(Color.FromRgb(100, 48, 120), 1.0)
                    }
                }),
                new GradientPreset("temperature", new LinearGradientBrush() {
                  StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                      new GradientStop(Color.FromRgb(30, 92, 179), 0.0),
                      new GradientStop(Color.FromRgb(30, 92, 179), 0.055),
                      new GradientStop(Color.FromRgb(23, 111, 193), 0.055),
                      new GradientStop(Color.FromRgb(23, 111, 193), 0.1117),
                      new GradientStop(Color.FromRgb(11, 142, 216), 0.1117),
                      new GradientStop(Color.FromRgb(11, 142, 216), 0.16670000000000001),
                      new GradientStop(Color.FromRgb(4, 161, 230), 0.16670000000000001),
                      new GradientStop(Color.FromRgb(4, 161, 230), 0.2217),
                      new GradientStop(Color.FromRgb(25, 181, 241), 0.2217),
                      new GradientStop(Color.FromRgb(25, 181, 241), 0.2783),
                      new GradientStop(Color.FromRgb(51, 188, 207), 0.2783),
                      new GradientStop(Color.FromRgb(51, 188, 207), 0.3333),
                      new GradientStop(Color.FromRgb(102, 204, 206), 0.3333),
                      new GradientStop(Color.FromRgb(102, 204, 206), 0.3883),
                      new GradientStop(Color.FromRgb(153, 219, 184), 0.3883),
                      new GradientStop(Color.FromRgb(153, 219, 184), 0.445),
                      new GradientStop(Color.FromRgb(192, 229, 136), 0.445),
                      new GradientStop(Color.FromRgb(192, 229, 136), 0.5),
                      new GradientStop(Color.FromRgb(204, 230, 75), 0.5),
                      new GradientStop(Color.FromRgb(204, 230, 75), 0.555),
                      new GradientStop(Color.FromRgb(243, 240, 29), 0.555),
                      new GradientStop(Color.FromRgb(243, 240, 29), 0.6117),
                      new GradientStop(Color.FromRgb(254, 222, 39), 0.6117),
                      new GradientStop(Color.FromRgb(254, 222, 39), 0.6667000000000001),
                      new GradientStop(Color.FromRgb(252, 199, 7), 0.6667000000000001),
                      new GradientStop(Color.FromRgb(252, 199, 7), 0.7217),
                      new GradientStop(Color.FromRgb(248, 157, 14), 0.7217),
                      new GradientStop(Color.FromRgb(248, 157, 14), 0.7783),
                      new GradientStop(Color.FromRgb(245, 114, 21), 0.7783),
                      new GradientStop(Color.FromRgb(245, 114, 21), 0.8332999999999999),
                      new GradientStop(Color.FromRgb(241, 71, 28), 0.8332999999999999),
                      new GradientStop(Color.FromRgb(241, 71, 28), 0.8883),
                      new GradientStop(Color.FromRgb(219, 30, 38), 0.8883),
                      new GradientStop(Color.FromRgb(219, 30, 38), 0.945),
                      new GradientStop(Color.FromRgb(164, 38, 44), 0.945),
                      new GradientStop(Color.FromRgb(164, 38, 44), 1.0)
                    }
                })
            };
        }
    }
}