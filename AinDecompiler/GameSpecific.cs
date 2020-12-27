using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler
{
    public static class GameSpecific
    {
        public static void DoRance01(AinFile ainFile, TextImportExport exportImport)
        {
            //TEMPORARY CODE FOR RANCE 01 NAME EXTRACTION
            var dic = new Dictionary<string, string>();
            dic["＊"] = "*";
            dic["＊／Ｂ"] = "*";
            dic["＊＊"] = "**";
            dic["＊＊／Ｂ"] = "**";
            dic["＊＊＊"] = "***";
            dic["ＴＡＤＡ"] = "TADA";
            dic["アイ"] = "Ai";
            dic["アキ"] = "Aki";
            dic["アリス"] = "Alice";
            dic["イェリコ"] = "Yeriko";
            dic["ウィリス"] = "Willis";
            dic["ウェンズディング"] = "Wendsding";
            dic["ウェンズディング／Ｂ"] = "Wendsding/B";
            dic["エンエン"] = "Enen";
            dic["カルピス"] = "Calpis";
            dic["キース"] = "Keith";
            dic["クリン"] = "Kurin";
            dic["シィル"] = "Sill";
            dic["ジャン"] = "Jean";
            dic["スアマ"] = "Suama";
            dic["ニウ"] = "Niu";
            dic["ネカイ"] = "Nekai";
            dic["ハイジ"] = "Heidi";
            dic["パティ"] = "Pattie";
            dic["パルプテンクス"] = "Pulptenks";
            dic["ヒカリ"] = "Hikari";
            dic["ブリティシュ"] = "British";
            dic["ボブザ"] = "Bobza";
            dic["マリス"] = "Maris";
            dic["ミ"] = "Mi";
            dic["ミリー"] = "Milly";
            dic["ムララ"] = "Murara";
            dic["メナド"] = "Menad";
            dic["ユキ"] = "Yuki";
            dic["ユラン"] = "Yulang";
            dic["ライハルト"] = "Reichardt";
            dic["ラベンダー"] = "Lavender";
            dic["ランス"] = "Rance";
            dic["リア"] = "Lia";
            dic["ルイス"] = "Luis";
            dic["魚介"] = "Gyokai";
            dic["奈美"] = "Nami";
            dic["忍者"] = "Ninja";
            dic["美樹"] = "Miki";
            dic["変態ネズミ"] = "Pervert Mouse";
            dic["諭吉"] = "Yukichi";
            dic["葉月"] = "Hazuki";

            dic["ＤＪＣ＋＋"] = "DJC++";
            dic["ＴＡＤＡ／１"] = "TADA";
            dic["ＴＡＤＡ／２"] = "TADA";
            dic["ＴＡＤＡ／３"] = "TADA";
            dic["ＴＡＤＡ／ウィリス"] = "Willis";
            dic["ＴＡＤＡ／ハイジ"] = "Heidi";
            dic["ＴＡＤＡ／パティ"] = "Pattie";
            dic["ＴＡＤＡ／パルプテンクス"] = "Pulptenks";
            dic["ＴＡＤＡ／ラベンダー"] = "Lavender";
            dic["ＴＡＤＡ／葉月"] = "Hazuki";
            dic["ふみゃ"] = "Fumya";
            dic["アイ／基本"] = "Ai";
            dic["アキ／基本"] = "Aki";
            dic["アリスマン／基本"] = "Alice man";
            dic["イェリコ／基本"] = "Yeriko";
            dic["イェリコ／通せんぼ"] = "Yeriko";
            dic["ウィリス／基本"] = "Willis";
            dic["キース／基本／遠距離"] = "Keith";
            dic["クリン／基本"] = "Kurin";
            dic["シィル／学生服"] = "Sill";
            dic["シィル／基本"] = "Sill";
            dic["ジャン／基本"] = "Jean";
            dic["ネカイ／基本"] = "Nekai";
            dic["ハイジ／基本"] = "Heidi";
            dic["ハイジ／基本／盗む"] = "Heidi";
            dic["パティ／基本"] = "Pattie";
            dic["パルプテンクス／基本"] = "Pulptenks";
            dic["パルプテンクス／救出前"] = "Pulptenks";
            dic["ヒカリ／基本"] = "Hikari";
            dic["ブリティシュ／基本"] = "British";
            dic["ブリティシュ／基本／泣く"] = "British";
            dic["ボブザ／基本"] = "Bobza";
            dic["マリス／学生服"] = "Maris";
            dic["マリス／基本"] = "Maris";
            dic["ミ／基本"] = "Mi";
            dic["ミリー／眼鏡"] = "Milly";
            dic["ミリー／眼帯"] = "Milly";
            dic["ミリー／基本"] = "Milly";
            dic["ムララ／基本"] = "Murara";
            dic["メナド／基本"] = "Menad";
            dic["ユキ／基本"] = "Yuki";
            dic["ユラン／基本"] = "Yulang";
            dic["ユラン／私服"] = "Yulang";
            dic["ユラン／私服／レイプ後"] = "Yulang";
            dic["ラベンダー／基本"] = "Lavender";
            dic["ラベンダー／基本／泣く"] = "Lavender";
            dic["リア／基本"] = "Lia";
            dic["ルイス／基本"] = "Luis";
            dic["魚介"] = "Gyokai";
            dic["都市守備隊／基本"] = "City defense corps";
            dic["都市守備隊／基本／Ｂ"] = "City defense corps";
            dic["盗賊団／基本"] = "Band of thieves";
            dic["盗賊団／基本／Ｂ"] = "Band of thieves";
            dic["奈美／基本"] = "Nami";
            dic["忍者／学生服"] = "Ninja";
            dic["忍者／学生服／驚く"] = "Ninja";
            dic["忍者／学生服／笑う"] = "Ninja";
            dic["忍者／基本"] = "Ninja";
            dic["汎用女生徒／基本"] = "Generic schoolgirl";
            dic["汎用女生徒／基本／Ｂ"] = "Generic schoolgirl";
            dic["美樹／基本"] = "Miki";
            dic["変態ネズミ／基本"] = "Pervert Mouse";
            dic["葉月／ボディペイント"] = "Hazuki";
            dic["葉月／ボディペイント／笑う"] = "Hazuki";
            dic["葉月／基本"] = "Hazuki";
            dic["葉月／基本／笑う"] = "Hazuki";
            dic["葉月／脱ぐ０４"] = "Hazuki";
            dic["葉月／脱ぐ０４／笑う"] = "Hazuki";
            dic["葉月／脱ぐ０６"] = "Hazuki";
            dic["葉月／脱ぐ０６／笑う"] = "Hazuki";
            dic["葉月／脱ぐ０７"] = "Hazuki";
            dic["葉月／脱ぐ０７／笑う"] = "Hazuki";
            dic["葉月／脱ぐ０８"] = "Hazuki";
            dic["葉月／脱ぐ０８／笑う"] = "Hazuki";

            var func = ainFile.GetFunction("●名札");
            var parameter = func.Parameters[0];

            var func2 = ainFile.GetFunction("●立ち絵");
            var parameter2 = func2.Parameters[0];

            exportImport.AnnotateParameterWithStrings(dic, parameter, parameter2);
        }

    }
}
