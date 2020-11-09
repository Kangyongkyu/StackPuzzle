﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuWaitMatch : MonoBehaviour
{
    private const string UIObjName = "CanvasPopUp/MenuWaitMatch";
    private bool mIsSearching = false;

    public Text State;
    public GameObject BtnMatch;
    public GameObject BtnCancle;

    public static void PopUp(bool autoPlay = false)
    {
        GameObject menuMatch = GameObject.Find("UIGroup").transform.Find(UIObjName).gameObject;
        MenuWaitMatch menu = menuMatch.GetComponent<MenuWaitMatch>();
        menu.ResetMatchUI();
        menuMatch.SetActive(true);
        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectButton2);

        if (autoPlay)
            menu.StartCoroutine(menu.AutoMatch());
    }

    public void OnClose()
    {
        if(mIsSearching)
        {
            SearchOpponentInfo info = new SearchOpponentInfo();
            info.userPk = UserSetting.UserPK;
            NetClientApp.GetInstance().Request(NetCMD.StopMatching, info, null);
        }

        mIsSearching = false;
        gameObject.SetActive(false);
        MenuMain.PopUp();
        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectButton1);
    }

    public void OnCancle()
    {
        mIsSearching = false;
        StopCoroutine("WaitOpponent");
        State.text = "Match Ready";
        BtnCancle.SetActive(false);
        BtnMatch.SetActive(true);
        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectButton1);

        SearchOpponentInfo info = new SearchOpponentInfo();
        info.userPk = UserSetting.UserPK;
        NetClientApp.GetInstance().Request(NetCMD.StopMatching, info, null);
    }

    public void OnMatch()
    {
        if(NetClientApp.GetInstance().IsDisconnected())
        {
            MenuMessageBox.PopUp("Network Disconnected", false, null);
            return;
        }

        UserInfo userInfo = UserSetting.LoadUserInfo();
        if (userInfo.userPk <= 0)
            StartCoroutine(WaitForAddingUserInfo());
        else
            RequestMatch();

        mIsSearching = true;
        BtnCancle.SetActive(true);
        BtnMatch.SetActive(false);
        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectButton1);

        StartCoroutine("WaitOpponent");
    }

    IEnumerator WaitOpponent()
    {
        int n = 0;
        while(true)
        {
            switch(n%3)
            {
                case 0: State.text = "Matching.."; break;
                case 1: State.text = "Matching..."; break;
                case 2: State.text = "Matching...."; break;
            }
            n++;
            yield return new WaitForSeconds(1);
        }
    }

    IEnumerator WaitForAddingUserInfo()
    {
        int limit = 60;
        while(0 < limit--)
        {
            yield return new WaitForSeconds(1);
            if (UserSetting.UserPK > 0)
            {
                RequestMatch();
                break;
            }
        }
    }

    private void RequestMatch()
    {
        SearchOpponentInfo info = new SearchOpponentInfo();
        info.userPk = UserSetting.UserPK;
        info.colorCount = 4.0f; // 4~6.0f
        info.oppUser = null;
        info.oppColorCount = 0;
        info.isDone = false;
        NetClientApp.GetInstance().Request(NetCMD.SearchOpponent, info, (_res) =>
        {
            SearchOpponentInfo res = _res as SearchOpponentInfo;
            if (res.isDone && mIsSearching)
            {
                if (res.oppUser == null)
                {
                    FailMatch();

                    if (UserSetting.IsBotPlayer)
                        StartCoroutine(AutoMatch());
                }
                else
                    SuccessMatch(res);
            }
            return;
        });
    }

    private void SuccessMatch(SearchOpponentInfo matchInfo)
    {
        mIsSearching = false;
        StopCoroutine("WaitOpponent");
        State.text = "Matched Player : " + matchInfo.oppUser.userPk;

        InitFieldInfo info = new InitFieldInfo();
        info.XCount = 5;
        info.YCount = 9;

        info.userPk = UserSetting.UserPK;
        NetClientApp.GetInstance().Request(NetCMD.GetInitField, info, (_res) =>
        {
            InitFieldInfo res = _res as InitFieldInfo;
            BattleFieldManager.Me.StartGame(res.userPk, res.XCount, res.YCount, res.products, matchInfo.colorCount);
        });

        info.userPk = matchInfo.oppUser.userPk;
        NetClientApp.GetInstance().Request(NetCMD.GetInitField, info, (_res) =>
        {
            InitFieldInfo res = _res as InitFieldInfo;
            BattleFieldManager.Opp.StartGame(res.userPk, res.XCount, res.YCount, res.products, matchInfo.colorCount);
        });

        MenuStages.Hide();
        StageManager.Inst.Activate(false);
        gameObject.SetActive(false);
    }
    private void FailMatch()
    {
        mIsSearching = false;
        StopCoroutine("WaitOpponent");
        State.text = "Match Failed";
        BtnCancle.SetActive(false);
        BtnMatch.SetActive(true);
    }
    private IEnumerator AutoMatch()
    {
        yield return new WaitForSeconds(1);
        OnMatch();
    }
    private void ResetMatchUI()
    {
        mIsSearching = false;
        BtnCancle.SetActive(false);
        BtnMatch.SetActive(true);
        State.text = "Match Ready";
        StopCoroutine("WaitOpponent");

    }
}
