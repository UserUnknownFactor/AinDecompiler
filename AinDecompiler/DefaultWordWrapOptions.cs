using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler
{
    public static class DefaultWordWrapOptions
    {
        public static WordWrapOptions GetWordWrapOptions(AinFile ainFile)
        {
            var wordWrapOptions = WordWrapOptions.LoadWordWrapOptions(ainFile);
            if (wordWrapOptions != null)
            {
                return wordWrapOptions;
            }
            wordWrapOptions = DefaultWordWrapOptions.DetectGame(ainFile);
            if (wordWrapOptions != null)
            {
                return wordWrapOptions;
            }
            wordWrapOptions = new WordWrapOptions();
            wordWrapOptions.Disabled = true;
            return wordWrapOptions;
        }

        public static WordWrapOptions DetectGame(AinFile ainFile)
        {
            WordWrapOptions wordWrapOptions = null;

            //Detect games
            TryDaibanchou(ainFile, ref wordWrapOptions);
            TrySengokuRance(ainFile, ref wordWrapOptions);
            TryDalkGaiden(ainFile, ref wordWrapOptions);
            TryRance6(ainFile, ref wordWrapOptions);
            TryDungeonsAndDolls(ainFile, ref wordWrapOptions);
            TryGalzooIsland(ainFile, ref wordWrapOptions);
            TryTsumaShibori(ainFile, ref wordWrapOptions);
            TryHoken(ainFile, ref wordWrapOptions);
            //TryDaiteikoku(ainFile, ref wordWrapOptions);
            TryHaruurare(ainFile, ref wordWrapOptions);
            //TryRance02(ainFile, ref wordWrapOptions);
            //TryRanceQuest(ainFile, ref wordWrapOptions);
            //TryOyakoRankan(ainFile, ref wordWrapOptions);
            //TryDrapeko(ainFile, ref wordWrapOptions);
            //TryRance01(ainFile, ref wordWrapOptions);
            //TryRance9(ainFile, ref wordWrapOptions);

            //if (wordWrapOptions != null)
            //{
            //    //generate default code for next line and next message commands
            //    wordWrapOptions.NextLineFunctionCode = GetGeneratedCode(wordWrapOptions.NextLineFunctionName, ainFile);
            //    wordWrapOptions.NextMessageFunctionCode = GetGeneratedCode(wordWrapOptions.NextMessageFunctionName, ainFile);
            //}


            ////Face portrait detection for a few games

            ////hoken
            //MatchFunction(ainFile, wordWrapOptions, 57, 41, "顔表示", DataType.Int);

            ////tsumashibori
            //MatchFunction(ainFile, wordWrapOptions, 57, 42, "顔", DataType.Int, DataType.Int);

            ////rancequest
            //if (MatchFunction(ainFile, wordWrapOptions, 57, 42, "セリフ２", DataType.Int, DataType.String) &&
            //    MatchFunction(ainFile, wordWrapOptions, 57, 42, "セリフ", DataType.Int, DataType.String) &&
            //    MatchFunction(ainFile, wordWrapOptions, 57, 42, "ト書き") &&
            //    MatchFunction(ainFile, wordWrapOptions, 57, 42, "思考", DataType.Int, DataType.String))
            //{
            //    wordWrapOptions.ReduceMarginFunctionName = new string[] { "セリフ", "セリフ２", "ト書き", "思考" }.Join(Environment.NewLine);
            //    wordWrapOptions.UseVariableWidthFont = true;
            //}

            ////oyakoranken
            //if (MatchFunction(ainFile, wordWrapOptions, -1, -1, "◎台詞", DataType.String, DataType.String))
            //{
            //    wordWrapOptions.UseVariableWidthFont = true;
            //}

            return wordWrapOptions;
        }

        private static void TryRance02(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            return;
        }

        private static void TryHaruurare(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            //メッセージエリア(1, 216, 408, 4, 15001);  ▲ト書き  //712px wide, 14 pixels chars, 50 chars wide, 3 lines in text box
            //メッセージエリア(2, 216, 408, 4, 15011);
            //メッセージエリア(3, 216, 408, 4, 15012);
            //メッセージエリア(4, 216, 408, 4, 15021);
            //メッセージエリア(5, 216, 408, 4, 15022);
            //メッセージエリア(6, -72, -56, 4, 15001);  ▲ト書き左上  //735px wide, 14 pixels chars, 51 chars wide, 3 lines in text box
            //メッセージエリア(7, 216, -56, 4, 15001);  ▲ト書き右上
            //メッセージエリア(8, -72, 512, 4, 15001);  ▲ト書き左下
            //メッセージエリア(9, 216, 512, 4, 15001);  ▲ト書き右下
            //void メッセージエリア(int Ｍ番号, int Ｘ, int Ｙ, int Ｚ, int ＣＧ番号)
            //void 字枠(int Ｍ番号, int 動作, int 効果番号, int 効果時間)
            //▲字枠(0, -1);

            if (ContainsFunction(ainFile, "▲ト書き") && ContainsFunction(ainFile, "▲ト書き左上") && ContainsFunction(ainFile, "▲ト書き右上") &&
                ContainsFunction(ainFile, "▲ト書き左下") && ContainsFunction(ainFile, "▲ト書き右下") &&
                ContainsFunction(ainFile, "メッセージエリア", DataType.Int, "Ｍ番号", DataType.Int, "Ｘ", DataType.Int, "Ｙ", DataType.Int, "Ｚ", DataType.Int, "ＣＧ番号") &&
                ContainsFunction(ainFile, "字枠", DataType.Int, "Ｍ番号", DataType.Int, "動作", DataType.Int, "効果番号", DataType.Int, "効果時間"))
            {
                wordWrapOptions = new WordWrapOptions();
                var profile = wordWrapOptions.WordWrapOptionsProfiles[0];
                profile.MaxLinesPerMessage = 3;
                profile.MaxCharactersPerLine = 50;

                profile = wordWrapOptions.WordWrapOptionsProfiles[1];
                profile.MaxLinesPerMessage = 3;
                profile.MaxCharactersPerLine = 50;
                profile.SetTriggerCodes(ainFile,
                    "CALLFUNC ▲ト書き",
                    "CALLFUNC ▲ト書き右上",
                    "CALLFUNC ▲ト書き右下");

                profile = wordWrapOptions.WordWrapOptionsProfiles[2];
                profile.ProfileName = "Left Side";
                profile.MaxLinesPerMessage = 3;
                profile.MaxCharactersPerLine = 51;
                profile.SetTriggerCodes(ainFile,
                    "CALLFUNC ▲ト書き左上",
                    "CALLFUNC ▲ト書き左下");
            }

            return;
        }

        private static void TryRance9(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            return;
        }

        private static void TryRance01(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            return;
        }

        private static void TryDrapeko(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            return;
        }

        private static void TryOyakoRankan(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            return;
        }

        private static void TryRanceQuest(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            return;
        }

        private static void TryDaiteikoku(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            return;
        }

        private static void TryHoken(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            //void 顔表示(int nCG_NUM)
            //void ＡＤＶ設定(int nBG_NUM, int nEffect_Num, int nEffect_Time)
            //void 全表示(int 効果番号, int 効果時間)
            if (ContainsFunction(ainFile, "顔表示", DataType.Int, "nCG_NUM") &&
                ContainsFunction(ainFile, "ＡＤＶ設定", DataType.Int, "nBG_NUM", DataType.Int, "nEffect_Num", DataType.Int, "nEffect_Time") &&
                ContainsFunction(ainFile, "全表示", DataType.Int, "効果番号", DataType.Int, "効果時間"))
            {
                wordWrapOptions = new WordWrapOptions();
                var profile = wordWrapOptions.WordWrapOptionsProfiles[0];
                profile.MaxLinesPerMessage = 3;
                profile.MaxCharactersPerLine = 57;

                profile = wordWrapOptions.WordWrapOptionsProfiles[1];
                profile.MaxLinesPerMessage = 3;
                profile.MaxCharactersPerLine = 57;
                profile.SetTriggerCodes(ainFile, "CALLFUNC A");

                profile = wordWrapOptions.WordWrapOptionsProfiles[2];
                profile.MaxLinesPerMessage = 3;
                profile.MaxCharactersPerLine = 41;
                profile.SetTriggerCodes(ainFile, "CALLFUNC 顔表示");
            }
            return;
        }

        private static void TryTsumaShibori(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            //会話画面@0
            //void 顔(int ＣＧ番号, int n名札)
            if (ContainsFunction(ainFile, "会話画面@0") &&
                ContainsFunction(ainFile, "顔", DataType.Int, "ＣＧ番号", DataType.Int, "n名札"))
            {
                wordWrapOptions = new WordWrapOptions();
                var profile = wordWrapOptions.WordWrapOptionsProfiles[0];
                profile.MaxLinesPerMessage = 3;
                profile.MaxCharactersPerLine = 57;  //this extends all the way to the decorative edge

                profile = wordWrapOptions.WordWrapOptionsProfiles[1];
                profile.MaxLinesPerMessage = 3;
                profile.MaxCharactersPerLine = 57;
                profile.SetTriggerCodes(ainFile, "CALLFUNC A");

                profile = wordWrapOptions.WordWrapOptionsProfiles[2];
                profile.MaxLinesPerMessage = 3;
                profile.MaxCharactersPerLine = 43;  //this extends all the way to the decorative edge
                profile.SetTriggerCodes(ainFile, "CALLFUNC 顔");
            }
            return;
        }

        private static void TryGalzooIsland(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            //void ト書き枠(int nEffectNo, int nSpeed)
            //void ＳＰ台詞枠(int nEffectNo, int nSpeed)
            //void 顔枠(int nEffectNo, int nSpeed)
            //void ＳＰ思考枠(int nEffectNo, int nSpeed)
            //void 顔思考枠(int nEffectNo, int nSpeed)
            //void ビジュアル枠(int nEffectNo, int nSpeed)
            //void 全画面枠(int nEffectNo, int nSpeed)
            //void 捕獲枠(int nCharaNo)
            //void ys迷宮環境設定_ポリゴン描画設定(int nSelect)
            //枠(91, true, 0, 0);

            if (ContainsFunction(ainFile, "ト書き枠", DataType.Int, "nEffectNo", DataType.Int, "nSpeed") &&
                ContainsFunction(ainFile, "ＳＰ台詞枠", DataType.Int, "nEffectNo", DataType.Int, "nSpeed") &&
                ContainsFunction(ainFile, "顔枠", DataType.Int, "nEffectNo", DataType.Int, "nSpeed") &&
                ContainsFunction(ainFile, "ＳＰ思考枠", DataType.Int, "nEffectNo", DataType.Int, "nSpeed") &&
                ContainsFunction(ainFile, "顔思考枠", DataType.Int, "nEffectNo", DataType.Int, "nSpeed") &&
                ContainsFunction(ainFile, "ビジュアル枠", DataType.Int, "nEffectNo", DataType.Int, "nSpeed") &&
                ContainsFunction(ainFile, "全画面枠", DataType.Int, "nEffectNo", DataType.Int, "nSpeed") &&
                ContainsFunction(ainFile, "捕獲枠", DataType.Int, "nCharaNo") &&
                ContainsFunction(ainFile, "ys迷宮環境設定_ポリゴン描画設定", DataType.Int, "nSelect"))
            {
                wordWrapOptions = new WordWrapOptions();
                var profile = wordWrapOptions.WordWrapOptionsProfiles[0];
                profile.MaxCharactersPerLine = 40;
                profile.MaxLinesPerMessage = 65535;

                profile = wordWrapOptions.WordWrapOptionsProfiles[1];
                profile.MaxCharactersPerLine = 30;
                profile.MaxLinesPerMessage = 4;
                profile.SetTriggerCodes(ainFile,
                    "CALLFUNC ト書き枠",
                    "CALLFUNC ＳＰ台詞枠",
                    "CALLFUNC 顔枠",
                    "CALLFUNC ＳＰ思考枠",
                    "CALLFUNC 顔思考枠"
                    );

                profile = wordWrapOptions.WordWrapOptionsProfiles[2];
                profile.ProfileName = "Visual";
                profile.MaxCharactersPerLine = 40;
                profile.MaxLinesPerMessage = 3;
                profile.SetTriggerCodes(ainFile, "CALLFUNC ビジュアル枠");

                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "Fullscreen";
                profile.MaxCharactersPerLine = 96;
                profile.MaxLinesPerMessage = 31;
                profile.SetTriggerCodes(ainFile, "CALLFUNC 全画面枠");

                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "Catch";
                profile.MaxCharactersPerLine = 33;
                profile.MaxLinesPerMessage = 4;
                profile.SetTriggerCodes(ainFile, "CALLFUNC 捕獲枠");

                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "Polygon drawing settings";
                profile.MaxCharactersPerLine = 47;
                profile.MaxLinesPerMessage = 12;
                profile.SetTriggerCodes(ainFile, "枠(91, true, 0, 0);");

                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "Font size 16";
                profile.MaxCharactersPerLine = 52;
                profile.MaxLinesPerMessage = 7;
                profile.SetTriggerCodes(ainFile, "PUSH 16 CALLFUNC 文字サイズ");

                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "Font size 18";
                profile.MaxCharactersPerLine = 46;
                profile.MaxLinesPerMessage = 6;
                profile.SetTriggerCodes(ainFile, "PUSH 18 CALLFUNC 文字サイズ");

                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "Font size 20";
                profile.MaxCharactersPerLine = 42;
                profile.MaxLinesPerMessage = 6;
                profile.SetTriggerCodes(ainFile, "PUSH 20 CALLFUNC 文字サイズ");

                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "PREVIOUS PROFILE";
                profile.MaxCharactersPerLine = 65535;
                profile.MaxLinesPerMessage = 65535;
                profile.SetTriggerCodes(ainFile, "PUSH -1 CALLFUNC 文字サイズ");
            }

            //default font size: 28

            //1	    ト書き枠	 stage directions frame  (425x128), 30x4
            //2	    ＳＰ台詞枠	 SP Speech frame         (425x128), 30x4
            //3	    顔枠	     face frame              (425x128), 30x4
            //4	    ＳＰ思考枠	 SP Idea frame           (425x128), 30x4
            //5	    顔思考枠	 face idea frame         (425x128), 30x4
            //6	    ビジュアル枠 Visual frame            (563x96),  40,3
            //11    全画面枠     Fullscreen frame        96x31
            //12    捕獲枠       catch frame             (473x128), 33x4
            //91    ys迷宮環境設定_ポリゴン描画設定 polygon drawing settings   47x12

            ///////////////////////////no, cg,  z,    x,   y,   w,   h,   r, g, b, bl, tx, ty,tx2,ty2,lsp,fontsize,fontface,fontbold,r,g,b,l,u,r,d,r,g,b,icon,nameflag
            //this.m_asData[idx++].set(1, 1005, 1100, 240, 390, 560, 210, 0, 0, 0, 0,  70, 60, 70, 60, 2, 28, 0, 0, 255, 255, 255, 1, 1, 1, 1, 0, 0, 0, 1, 0);
            //this.m_asData[idx++].set(2, 1006, 1100, 240, 390, 560, 210, 0, 0, 0, 0,  70, 60, 70, 60, 2, 28, 0, 0, 255, 255, 255, 1, 1, 1, 1, 0, 0, 0, 1, 0);
            //this.m_asData[idx++].set(3, 1007, 1100, 240, 390, 560, 210, 0, 0, 0, 0,  70, 60, 70, 60, 2, 28, 0, 0, 255, 255, 255, 1, 1, 1, 1, 0, 0, 0, 1, 0);
            //this.m_asData[idx++].set(4, 1008, 1100, 240, 390, 560, 210, 0, 0, 0, 0,  70, 60, 70, 60, 2, 28, 0, 0, 255, 255, 255, 1, 1, 1, 1, 0, 0, 0, 1, 0);
            //this.m_asData[idx++].set(5, 1009, 1100, 240, 390, 560, 210, 0, 0, 0, 0,  70, 60, 70, 60, 2, 28, 0, 0, 255, 255, 255, 1, 1, 1, 1, 0, 0, 0, 1, 0);
            //this.m_asData[idx++].set(6, 1010, 1100, 0,   470, 800, 130, 0, 0, 0, 0, 115, 20,115, 20, 2, 28, 0, 0, 255, 255, 255, 1, 1, 1, 1, 0, 0, 0, 2, 0);
            //this.m_asData[idx++].set(11, 0,   1200, 0,     0, 800, 600, 0, 0, 0,255, 16, 16, 16, 16, 2, 16, 0, 0, 255, 255, 255, 0, 0, 0, 0, 0, 0, 0, 3, 0);
            //this.m_asData[idx++].set(12, 1916,1100, 0,   420, 800, 180, 0, 0, 0, 0, 260, 30,260, 30, 2, 28, 0, 0, 255, 255, 255, 1, 1, 1, 1, 0, 0, 0, 1, 0);
            //this.m_asData[idx++].set(91, 0,   2000, 110, 136, 580, 324, 0, 0, 0, 64, 16, 16, 16, 16, 2, 24, 0, 0, 255, 255, 255, 2, 2, 2, 0, 0, 0, 0, 3, 0);
            //this.m_asData[idx++].set(99, 0,   1100, 0,   420, 800, 130, 0, 0, 0, 0, 150, 20,150, 20, 2, 28, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            return;
        }

        private static void TryDungeonsAndDolls(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            //void SP_SET_TEXT_HOME(int nSP, int nX, int nY);
            //void 枠(int n枠番号, int n効果番号, int n所要時間);
            //bool CSpriteMsg::Build(int nCG, int nTextX, int nTextY, int nWidth, int nHeight, int nR, int nG, int nB, int nA);

            ////////////////////////cg,   tx, ty, w,   h,   r, g, b, a
            //g_tInfo.m_tMsgA.Build(1500, 39, 20, 640, 80,  0, 0, 0, 127);   text box 580x96, 41 chars wide
            //g_tInfo.m_tMsgB.Build(0,    13, 12, 512, 600, 0, 0, 0, 127);

            if (ContainsFunction(ainFile, "SP_SET_TEXT_HOME", DataType.Int, "nSP", DataType.Int, "nX", DataType.Int, "nY") &&
                ContainsFunction(ainFile, "枠", DataType.Int, "n枠番号", DataType.Int, "n効果番号", DataType.Int, "n所要時間") &&
                ContainsFunction(ainFile, "CSpriteMsg@Build", DataType.Int, "nCG", DataType.Int, "nTextX", DataType.Int, "nTextY",
                    DataType.Int, "nWidth", DataType.Int, "nHeight", DataType.Int, "nR", DataType.Int, "nG", DataType.Int, "nB", DataType.Int, "nA"))
            {
                wordWrapOptions = new WordWrapOptions();
                var profile = wordWrapOptions.WordWrapOptionsProfiles[0];
                profile.MaxCharactersPerLine = 41;
                profile.MaxLinesPerMessage = 3;

                profile = wordWrapOptions.WordWrapOptionsProfiles[1];
                profile.MaxCharactersPerLine = 41;
                profile.MaxLinesPerMessage = 3;
                profile.SetTriggerCodes(ainFile,
                    "枠(1, 0xDEADBEEF, 0xDEADBEEF, 0xDEADBEEF);"
                    );

                profile = wordWrapOptions.WordWrapOptionsProfiles[2];
                profile.ProfileName = "No Frame?";
                profile.MaxCharactersPerLine = 35;
                profile.MaxLinesPerMessage = 19;
                profile.SetTriggerCodes(ainFile,
                    "枠(2, 0xDEADBEEF, 0xDEADBEEF, 0xDEADBEEF);"
                    );

                profile = wordWrapOptions.WordWrapOptionsProfiles[3];
                profile.SetTriggerCodes(ainFile, "CALLFUNC SP_SET_TEXT_HOME");
            }


            return;
        }

        private static void TryRance6(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            //void 依頼書画面前処理();
            //void 枠(int n枠番号, int nキャラ番号, int n効果番号, int n所要時間)
            if (ContainsFunction(ainFile, "依頼書画面前処理") &&
                ContainsFunction(ainFile, "枠", DataType.Int, "n枠番号", DataType.Int, "nキャラ番号", DataType.Int, "n効果番号", DataType.Int, "n所要時間"))
            {
                wordWrapOptions = new WordWrapOptions();
                var profile = wordWrapOptions.WordWrapOptionsProfiles[0];
                profile.MaxLinesPerMessage = 65535;
                profile.MaxCharactersPerLine = 44;

                profile = wordWrapOptions.WordWrapOptionsProfiles[1];
                profile.ProfileName = "#4, 5, 6: Normal (3 lines)";
                profile.MaxLinesPerMessage = 3;
                profile.MaxCharactersPerLine = 44;
                profile.SetTriggerCodes(ainFile,
                    //"PUSH 4 PUSH 0xDEADBEEF PUSH 0xDEADBEEF PUSH 0xDEADBEEF CALLFUNC 枠",
                    //"PUSH 5 PUSH 0xDEADBEEF PUSH 0xDEADBEEF PUSH 0xDEADBEEF CALLFUNC 枠",
                    //"PUSH 6 PUSH 0xDEADBEEF PUSH 0xDEADBEEF PUSH 0xDEADBEEF CALLFUNC 枠"
                    "枠(4, 0xDEADBEEF, 0xDEADBEEF, 0xDEADBEEF);",
                    "枠(5, 0xDEADBEEF, 0xDEADBEEF, 0xDEADBEEF);",
                    "枠(6, 0xDEADBEEF, 0xDEADBEEF, 0xDEADBEEF);"
                    );

                profile = wordWrapOptions.WordWrapOptionsProfiles[2];
                profile.ProfileName = "#3: Stage directions (4 lines)";
                profile.MaxLinesPerMessage = 4;
                profile.MaxCharactersPerLine = 44;
                profile.SetTriggerCodes(ainFile,
                    //"PUSH 3 PUSH 0xDEADBEEF PUSH 0xDEADBEEF PUSH 0xDEADBEEF CALLFUNC 枠"
                    "枠(3, 0xDEADBEEF, 0xDEADBEEF, 0xDEADBEEF);"
                    );

                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "#7: Report";
                profile.MaxLinesPerMessage = 2;
                profile.MaxCharactersPerLine = 44;
                profile.SetTriggerCodes(ainFile,
                    //"PUSH 7 PUSH 0xDEADBEEF PUSH 0xDEADBEEF PUSH 0xDEADBEEF CALLFUNC 枠"
                    "枠(7, 0xDEADBEEF, 0xDEADBEEF, 0xDEADBEEF);"
                    );

                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "#8: Warning (?)";
                profile.MaxLinesPerMessage = 5;
                profile.MaxCharactersPerLine = 58;
                profile.SetTriggerCodes(ainFile,
                    //"PUSH 8 PUSH 0xDEADBEEF PUSH 0xDEADBEEF PUSH 0xDEADBEEF CALLFUNC 枠"
                    "枠(8, 0xDEADBEEF, 0xDEADBEEF, 0xDEADBEEF);"
                    );

                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "#9: 2 lines";
                profile.MaxLinesPerMessage = 2;
                profile.MaxCharactersPerLine = 46;
                profile.SetTriggerCodes(ainFile,
                    //"PUSH 9 PUSH 0xDEADBEEF PUSH 0xDEADBEEF PUSH 0xDEADBEEF CALLFUNC 枠"
                    "枠(9, 0xDEADBEEF, 0xDEADBEEF, 0xDEADBEEF);"
                    );

                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "#10: Explanation (?)";
                profile.MaxLinesPerMessage = 65535;
                profile.MaxCharactersPerLine = 78;
                profile.SetTriggerCodes(ainFile,
                    //"PUSH 10 PUSH 0xDEADBEEF PUSH 0xDEADBEEF PUSH 0xDEADBEEF CALLFUNC 枠"
                    "枠(10, 0xDEADBEEF, 0xDEADBEEF, 0xDEADBEEF);"
                    );

                //3 g_tト書き (stage directions)
                //4 g_t台詞 (speech)
                //5 g_t思惑 (speculation)
                //6 g_tＧＥＴ
                //7 g_t報告 (report)
                //8 g_t警告 (warning)
                //9 g_t２行 (2line)
                //10 g_t説明 (explanation)


                //3 stage directions
                //    g_tト書き.Build(2003, 54, 10);  about 528x108  44 x 4
                //    g_tト書き.SetPos(3, 355);
                //    fontsize 24  face 0
                //4 speech
                //    g_t台詞.Build(2004, 54, 6);  about 528x82   44 x 3
                //    g_t台詞.SetPos(3, 355);
                //    fontsize 24  face 0
                //5 speculation
                //    g_t思惑.Build(2005, 54, 6);  about 528x82   44 x 3
                //    g_t思惑.SetPos(3, 355);
                //    fontsize 24  face 0
                //6 GET
                //    g_tＧＥＴ.Build(2006, 62, 16);  about 512x96  44 x 3  (previously thought to be 42 x 3)
                //    g_tＧＥＴ.SetPos(3, 355);
                //    fontsize 24  face 0
                //7 report
                //    g_t報告.Build(2567, 56, 10, 640, 80, 0, 0, 0, 127);   about 528x56   44 x 2
                //    g_t報告.SetPos(3, 407);
                //    fontsize 24  face 0
                //8 warning
                //    g_t警告.Build(2028, 0, 0);    unknown, 58 x 5
                //    g_t警告.SetPos(0, 172);
                //    fontsize 22  face 0
                //9 2line
                //    g_t２行.Build(2706, 8, 42, 640, 80, 0, 0, 0, 127);  about 466x55     46 x 2
                //    g_t２行.SetPos(4, 380);
                //    fontsize 20  face 0
                //10 explanation
                //  g_t説明.Build(0, 8, 8, 640, 480, 20, 20, 20, 192);  unknown, 80 x 26
                //  g_t説明.SetPos(0, 0);
                //  fontsize 16  face 0


                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "Notes Screen";
                //font size: 14x16
                //dimensions of area:
                //approx 86,50, width = 470, height = 360
                profile.MaxCharactersPerLine = 74;
                profile.MaxLinesPerMessage = 65535;
                profile.SetTriggerCodes(ainFile, "CALLFUNC 依頼書画面前処理");

            }
            return;
        }

        private static void TryDalkGaiden(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            //DALK Gaiden:
            //void StructMessage::func293(int nNo, int nEffect, int nTime)
            if (ContainsFunction(ainFile, "StructMessage@func293", DataType.Int, "nNo", DataType.Int, "nEffect", DataType.Int, "nTime"))
            {
                wordWrapOptions = new WordWrapOptions();
                wordWrapOptions.IgnoreAngleBraces = true;
                //game is DALK Gaiden
                var profile = wordWrapOptions.WordWrapOptionsProfiles[0];
                profile.MaxCharactersPerLine = 46;
                profile.MaxLinesPerMessage = 3;

                profile = wordWrapOptions.WordWrapOptionsProfiles[1];
                profile.MaxCharactersPerLine = 46;
                profile.MaxLinesPerMessage = 3;
                profile.SetTriggerCodes(ainFile,
                    "Msg.func293(1, VISMES_EF, VISMES_EFW);",
                    "Msg.func293(2, VISMES_EF, VISMES_EFW);",
                    "Msg.func293(3, VISMES_EF, VISMES_EFW);",
                    "Msg.func293(4, VISMES_EF, VISMES_EFW);",
                    "Msg.func293(6, VISMES_EF, VISMES_EFW);",
                    "Msg.func293(7, VISMES_EF, VISMES_EFW);",
                    "Msg.func293(8, VISMES_EF, VISMES_EFW);");

                profile = wordWrapOptions.WordWrapOptionsProfiles[2];
                profile.ProfileName = "Wide 1";
                profile.MaxCharactersPerLine = 62;
                profile.MaxLinesPerMessage = 3;
                profile.SetTriggerCodes(ainFile,
                    "Msg.func293(5, VISMES_EF, VISMES_EFW);",
                    "Msg.func293(20, VISMES_EF, VISMES_EFW);");

                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "Wide 2";
                profile.MaxCharactersPerLine = 60;
                profile.MaxLinesPerMessage = 3;
                profile.SetTriggerCodes(ainFile,
                    "Msg.func293(9, VISMES_EF, VISMES_EFW);");

                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "Narrow";
                profile.MaxCharactersPerLine = 32;
                profile.MaxLinesPerMessage = 3;
                profile.SetTriggerCodes(ainFile,
                    "Msg.func293(10, VISMES_EF, VISMES_EFW);",
                    "Msg.func293(11, VISMES_EF, VISMES_EFW);");
            }
        }

        private static void TrySengokuRance(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            //Sengoku Rance:
            //44 wide
            //void 台詞枠(int nNameNo, int nEffectNo, int nSpeed)
            //void 思考枠(int nNameNo, int nEffectNo, int nSpeed)
            //void ト書き枠(int nEffectNo, int nSpeed)
            //void 全画面枠(int nEffectNo, int nSpeed)
            //void 全画面枠２(int nBlendRate, int nEffectNo, int nSpeed)
            //void 枠(int nType, int nNameNo, int nBlendRate, int nEffectNo, int nSpeed)
            if (ContainsFunction(ainFile, "台詞枠", DataType.Int, "nNameNo", DataType.Int, "nEffectNo", DataType.Int, "nSpeed") &&
                ContainsFunction(ainFile, "思考枠", DataType.Int, "nNameNo", DataType.Int, "nEffectNo", DataType.Int, "nSpeed") &&
                ContainsFunction(ainFile, "ト書き枠", DataType.Int, "nEffectNo", DataType.Int, "nSpeed") &&
                ContainsFunction(ainFile, "全画面枠", DataType.Int, "nEffectNo", DataType.Int, "nSpeed") &&
                ContainsFunction(ainFile, "全画面枠２", DataType.Int, "nBlendRate", DataType.Int, "nEffectNo", DataType.Int, "nSpeed") &&
                ContainsFunction(ainFile, "枠", DataType.Int, "nType", DataType.Int, "nNameNo", DataType.Int, "nBlendRate", DataType.Int, "nEffectNo", DataType.Int, "nSpeed"))
            {
                wordWrapOptions = new WordWrapOptions();
                //game is Sengoku Rance
                var profile = wordWrapOptions.WordWrapOptionsProfiles[0];
                profile.MaxCharactersPerLine = 44;
                profile.MaxLinesPerMessage = 3;

                profile = wordWrapOptions.WordWrapOptionsProfiles[1];
                profile.MaxCharactersPerLine = 44;
                profile.MaxLinesPerMessage = 3;
                profile.SetTriggerCodes(ainFile,
                    "CALLFUNC 台詞枠",
                    "CALLFUNC 思考枠",
                    "CALLFUNC ト書き枠");

                profile = wordWrapOptions.WordWrapOptionsProfiles[2];
                profile.ProfileName = "Fullscreen 1";
                profile.MaxCharactersPerLine = 96;
                profile.MaxLinesPerMessage = 31;
                profile.SetTriggerCodes(ainFile, "CALLFUNC 全画面枠");

                profile = new WordWrapOptionsProfile();
                wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                profile.ProfileName = "Fullscreen 2";
                profile.MaxCharactersPerLine = 62;
                profile.MaxLinesPerMessage = 21;
                profile.SetTriggerCodes(ainFile, "CALLFUNC 全画面枠２");
            }
        }

        private static void TryDaibanchou(AinFile ainFile, ref WordWrapOptions wordWrapOptions)
        {
            //Daibanchou:
            //void 枠(int nWin)
            //void Ｅ枠(int nWin, int nEffect, int nWait)
            //void StructLayer::Build(int nEffect, int nTime)
            //void StructMessageWindow::Open(int nWindow)

            if (ContainsFunction(ainFile, "枠", DataType.Int, "nWin") &&
                ContainsFunction(ainFile, "Ｅ枠", DataType.Int, "nWin", DataType.Int, "nEffect", DataType.Int, "nWait") &&
                ContainsFunction(ainFile, "StructLayer@Build", DataType.Int, "nEffect", DataType.Int, "nTime") &&
                ContainsFunction(ainFile, "StructMessageWindow@Open", DataType.Int, "nWindow"))
            {
                wordWrapOptions = new WordWrapOptions();
                wordWrapOptions.IgnoreAngleBraces = true;
                //game is Daibanchou
                var profile = wordWrapOptions.WordWrapOptionsProfiles[0];
                profile.ProfileName = "Default (wide)";
                profile.MaxCharactersPerLine = 50;
                profile.MaxLinesPerMessage = 3;

                profile = wordWrapOptions.WordWrapOptionsProfiles[1];
                profile.MaxCharactersPerLine = 38;
                profile.MaxLinesPerMessage = 3;
                profile.SetTriggerCodes(ainFile,
                    "PUSH 1 CALLFUNC 枠",
                    "PUSH 2 CALLFUNC 枠",
                    "PUSH 3 CALLFUNC 枠",
                    "PUSH 4 CALLFUNC 枠",
                    "PUSH 8 CALLFUNC 枠",
                    "PUSH 10 CALLFUNC 枠",
                    "PUSH 11 CALLFUNC 枠");

                profile = wordWrapOptions.WordWrapOptionsProfiles[2];
                profile.ProfileName = "Wide";
                profile.MaxCharactersPerLine = 50;
                profile.MaxLinesPerMessage = 3;
                profile.SetTriggerCodes(ainFile,
                    "PUSH 5 CALLFUNC 枠",
                    "PUSH 6 CALLFUNC 枠");
            }
        }

        //private static bool MatchFunction(AinFile ainFile, WordWrapOptions2 wordWrapOptions, int maxCharactersPerLineNormal, int maxCharactersPerLineReduced, string functionName, params DataType[] dataTypes)
        //{
        //    var function = ainFile.GetFunction(functionName);
        //    if (function != null && function.ParameterCount == dataTypes.Length && function.Parameters.Take(function.ParameterCount).Select(a => a.DataType).SequenceEqual(dataTypes))
        //    {
        //        //if we specified values for the max lengths, set them
        //        if (maxCharactersPerLineNormal > 0)
        //        {
        //            wordWrapOptions.MaxCharactersPerLineNormal = maxCharactersPerLineNormal;
        //        }
        //        if (maxCharactersPerLineReduced > 0)
        //        {
        //            wordWrapOptions.MaxCharactersPerLineReduced = maxCharactersPerLineReduced;
        //        }

        //        //set the function name
        //        wordWrapOptions.ReduceMarginFunctionName = functionName;

        //        //indicate success
        //        return true;
        //    }
        //    return false;
        //}

        private static bool ContainsFunction(AinFile ainFile, string functionName, params object[] dataTypesAndParamaterNames)
        {
            var func = ainFile.GetFunction(functionName);
            if (func == null)
            {
                return false;
            }
            if ((dataTypesAndParamaterNames.Length & 1) != 0)
            {
                throw new ArgumentException("Number of parameter for dataTypesAndParameterNames must be even");
            }

            if (func.ParameterCount == 0 && dataTypesAndParamaterNames.Length == 0)
            {
                return true;
            }

            for (int i = 0; i + 1 < dataTypesAndParamaterNames.Length; i += 2)
            {
                DataType? dataType = dataTypesAndParamaterNames[i] as DataType?;
                string parameterName = dataTypesAndParamaterNames[i + 1] as string;

                if (dataType == null || parameterName == null)
                {
                    throw new ArgumentException("Wrong type for data type or parameter name");
                }

                var arg = func.GetNonVoidFunctionParameter(i / 2);
                if (arg == null)
                {
                    return false;
                }

                if (dataType == arg.DataType && parameterName == arg.Name)
                {

                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        public static byte[] GetGeneratedCodeBytes(AinFile ainFile, string functionName)
        {
            string code = GetGeneratedCodeText(ainFile, functionName);
            byte[] bytes = AssemblerProjectReader.CompileCode(code, ainFile);
            return bytes;
        }

        public static string GetGeneratedCodeText(AinFile ainFile, string functionName)
        {
            StringBuilder sb = new StringBuilder();
            if (ainFile.FunctionNameToIndex.ContainsKey(functionName))
            {
                Function function = ainFile.Functions[ainFile.FunctionNameToIndex[functionName]];
                if (function.ParameterCount > 0)
                {
                    for (int i = 0; i < function.ParameterCount; i++)
                    {
                        var arg = function.Parameters[i];
                        if (arg.DataType == DataType.Int)
                        {
                            sb.AppendLine("\t" + "PUSH 0");
                        }
                        else if (arg.DataType == DataType.String)
                        {
                            sb.AppendLine("\t" + "S_PUSH \"\"");
                        }
                        else if (arg.DataType == DataType.Float)
                        {
                            sb.AppendLine("\t" + "F_PUSH 0.0");
                        }
                        else
                        {
                            sb.AppendLine("\t" + "PUSH 0");
                        }
                    }
                }
            }
            sb.Append("\t" + "CALLFUNC " + functionName);
            return sb.ToString();
        }
    }
}
