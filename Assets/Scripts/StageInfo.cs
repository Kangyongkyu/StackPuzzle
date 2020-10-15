﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Android;
public class StageInfoCell
{
    public int ProductChocoCount;
    public int FrameCoverCount;
    public StageInfoCell(int chocoCount, int CoverCount)
    {
        ProductChocoCount = chocoCount;
        FrameCoverCount = CoverCount;
    }
}
public class StageInfo
{
    public const int Version = 3;
    public int Num;
    public string GoalType;
    public int GoalValue;
    public Sprite GoalTypeImage;
    public int MoveLimit;
    public int ColorCount;
    public int XCount;
    public int YCount;
    public bool ItemOneMore;
    public bool ItemKeepCombo;
    public bool ItemSameColor;
    public bool ItemReduceColor;
    public StageInfoCell[,] Field;

    public static StageInfo Load(int stageNum)
    {
#if PLATFORM_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead))
            Permission.RequestUserPermission(Permission.ExternalStorageRead);
#endif

        //string fullname = GetPath() + stageNum + ".txt";
        //if(!File.Exists(fullname)) //최초 실행시 한번만 수행됨(각 스테이지 정보를 기록한 파일들 Save)
        //{
        //    CreateStageInfoFolder();
        //
        //    StageInfo defInfo = new StageInfo();
        //    defInfo.DefaultSetting(stageNum);
        //    Save(defInfo);
        //}
        //
        //string fileText = File.ReadAllText(fullname);
        //if (fileText == null || fileText.Length == 0)
        //    return null;

        TextAsset ta = Resources.Load<TextAsset>("StageInfo/Version"+ Version + "/" + stageNum);
        if (ta == null || ta.text.Length == 0)
            return null;


        StageInfo info = new StageInfo();
        info.Num = stageNum;

        int RowIndex = 0;
        string[] lines = ta.text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            string[] tokens = line.Split(',');
            if (line[0] == '#' || tokens.Length < 2)
                continue;

            switch(tokens[0])
            {
                case "GoalType": info.GoalType = tokens[1]; break;
                case "GoalValue": info.GoalValue = int.Parse(tokens[1]); break;
                case "MoveLimit": info.MoveLimit = int.Parse(tokens[1]); break;
                case "ColorCount": info.ColorCount = int.Parse(tokens[1]); break;
                case "XCount": info.XCount = int.Parse(tokens[1]); break;
                case "YCount": info.YCount = int.Parse(tokens[1]); RowIndex = info.YCount; break;
                case "ItemOneMore": info.ItemOneMore = bool.Parse(tokens[1]); break;
                case "ItemKeepCombo": info.ItemKeepCombo = bool.Parse(tokens[1]); break;
                case "ItemSameColor": info.ItemSameColor = bool.Parse(tokens[1]); break;
                case "ItemReduceColor": info.ItemReduceColor = bool.Parse(tokens[1]); break;
                case "Rows": info.ParseRow(--RowIndex, tokens[1]); break;
            }
        }

        info.GoalTypeImage = TypeToImage(info.GoalType);

        return info;
    }
    //public static void Save(StageInfo info)
    //{
    //    string text = "GoalType," + info.GoalType + "\r\n"
    //                + "GoalValue," + info.GoalValue.ToString() + "\r\n"
    //                + "MoveLimit," + info.MoveLimit.ToString() + "\r\n"
    //                + "ColorCount," + info.ColorCount.ToString() + "\r\n"
    //                + "XCount," + info.XCount.ToString() + "\r\n"
    //                + "YCount," + info.YCount.ToString() + "\r\n"
    //                + "ItemOneMore," + info.ItemOneMore + "\r\n"
    //                + "ItemKeepCombo," + info.ItemKeepCombo + "\r\n"
    //                + "ItemSameColor," + info.ItemSameColor + "\r\n"
    //                + "ItemReduceColor," + info.ItemReduceColor + "\r\n"
    //                ;
    //
    //    for(int yIdx = info.YCount - 1; yIdx >= 0; --yIdx)
    //        text += "Rows," + info.RowToString(yIdx);
    //
    //    File.WriteAllText(GetPath() + info.Num + ".txt", text);
    //}

    public static void CreateStageInfoFolder()
    {
        string sDirPath = GetPath();
        DirectoryInfo di = new DirectoryInfo(sDirPath);
        if (di.Exists == false)
            di.Create();
    }

    public static string GetPath()
    {
        return Application.persistentDataPath + "/StageInfo/Version" + Version + "/";
    }

    public static Sprite TypeToImage(string goalType)
    {
        Sprite image = null;
        switch(goalType)
        {
            case "Score": image = Resources.Load<Sprite>("Images/score"); break;
            case "Combo": image = Resources.Load<Sprite>("Images/combo"); break;
            case "ItemOneMore": image = Resources.Load<Sprite>("Images/itemOneMore"); break;
            case "ItemKeepCombo": image = Resources.Load<Sprite>("Images/itemKeepCombo"); break;
            case "ItemSameColor": image = Resources.Load<Sprite>("Images/itemSameColor"); break;
            case "Cover": image = Resources.Load<Sprite>("Images/cover"); break;
            case "Choco": image = Resources.Load<Sprite>("Images/choco"); break;
            default: break;
        }
        return image;
    }
    public void ParseRow(int rowIndex, string row)
    {
        if (Field == null)
            Field = new StageInfoCell[XCount, YCount];

        string[] columns = row.Trim().Split(' ');
        if (columns.Length != XCount || rowIndex < 0)
            return;

        for(int xIdx = 0; xIdx < columns.Length; ++xIdx)
        {
            string[] keyValue = columns[xIdx].Split('/');
            Field[xIdx, rowIndex] = new StageInfoCell(int.Parse(keyValue[0]), int.Parse(keyValue[1]));
        }
    }
    public string RowToString(int rowIndex)
    {
        if (Field == null)
            return "";

        string rowString = "";
        for (int xIdx = 0; xIdx < XCount; ++xIdx)
        {
            rowString += Field[xIdx, rowIndex].ProductChocoCount.ToString() + "/" + Field[xIdx, rowIndex].FrameCoverCount.ToString() + " ";
        }
        return rowString + "\r\n";
    }
    public StageInfoCell GetCell(int idxX, int idxY)
    {
        if (Field == null || idxX < 0 || idxY < 0 || idxX >= XCount || idxY >= YCount)
            return new StageInfoCell(0, 0);

        return Field[idxX, idxY];
    }

    //public void DefaultSetting(int level)
    //{
    //    Num = level;
    //    GoalType = "Score"; //Score, Combo, ItemOneMore, ItemKeepCombo, ItemSameColor, Cover, Choco
    //    GoalValue = 500;
    //    MoveLimit = 20;
    //    ColorCount = 4;
    //    XCount = 6;
    //    YCount = 6;
    //    ItemOneMore = false;
    //    ItemKeepCombo = false;
    //    ItemSameColor = false;
    //    ItemReduceColor = false;
    //    Field = new StageInfoCell[XCount, YCount];
    //    for (int y =0; y < YCount; ++y)
    //        for (int x = 0; x < XCount; ++x)
    //            Field[x, y] = new StageInfoCell(0, 0);
    //}
}
