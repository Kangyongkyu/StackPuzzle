﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class MenuInGame : MonoBehaviour
{
    private static MenuInGame mInst = null;
    private const string UIObjName = "MenuInGame";
    private StageInfo mStageInfo;
    private int mAddedScore;
    private int mCurrentScore;
    private List<GameObject> mScoreStars = new List<GameObject>();

    public Text CurrentScore;
    public Text KeepCombo;
    public Text CurrentComboDisplay;
    public Text Limit;
    public Text StageLevel;
    public Text TargetValue;
    public Image TargetType;
    public Image Lock;
    public Image UnLock;
    public Image ScoreBar1;
    public Image ScoreBar2;
    public NumbersUI ComboNumber;
    public GameObject ParentPanel;
    public GameObject ComboText;
    public GameObject GameField;
    public GameObject ItemPrefab;
    public GameObject ScoreStarPrefab;

    public int Score { get { return mCurrentScore + mAddedScore; } }

    private void Update()
    {
#if PLATFORM_ANDROID
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnPause();
        }
#endif
        UpdateScore();

        CheckFinish();
    }

    private void UpdateScore()
    {
        int scorePerBar = UserSetting.ScorePerBar;
        if (mAddedScore <= 0)
            return;

        if (mAddedScore < 30)
        {
            mCurrentScore += mAddedScore;
            mAddedScore = 0;
            int n = mCurrentScore % scorePerBar;
            ScoreBar1.fillAmount = n / (float)scorePerBar;
            ScoreBar2.gameObject.SetActive(false);
            CurrentScore.text = mCurrentScore.ToString();
            CurrentScore.GetComponent<Animation>().Play("touch");
        }
        else if ((mCurrentScore+mAddedScore)/ scorePerBar > mCurrentScore/ scorePerBar)
        {
            mCurrentScore += mAddedScore;
            mAddedScore = 0;
            int n = mCurrentScore % scorePerBar;
            ScoreBar1.fillAmount = n / (float)scorePerBar;
            ScoreBar2.gameObject.SetActive(false);
            CurrentScore.text = mCurrentScore.ToString();
            CurrentScore.GetComponent<Animation>().Play("touch");
        }
        else
        {
            StartCoroutine(ScoreBarEffect(mCurrentScore, mAddedScore));
            mCurrentScore += mAddedScore;
            mAddedScore = 0;
            int n = mCurrentScore % scorePerBar;
            CurrentScore.text = mCurrentScore.ToString();
            CurrentScore.GetComponent<Animation>().Play("touch");

        }

        FillScoreStar();
    }
    private void FillScoreStar()
    {
        int starCount = mCurrentScore / UserSetting.ScorePerBar;
        float pixelPerUnit = GetComponent<CanvasScaler>().referencePixelsPerUnit;
        float imgWidth = ScoreStarPrefab.GetComponent<Image>().sprite.rect.width / pixelPerUnit;
        float barWidth = ScoreBar1.GetComponent<Image>().sprite.rect.width / pixelPerUnit;
        Vector3 basePos = ScoreBar1.transform.position + new Vector3((imgWidth - barWidth) * 0.5f, 0.5f, 0);
        while (mScoreStars.Count < starCount)
        {
            basePos = ScoreBar1.transform.position + new Vector3((imgWidth - barWidth) * 0.5f, 0.5f, 0);
            basePos.x += (imgWidth * mScoreStars.Count);
            GameObject obj = GameObject.Instantiate(ScoreStarPrefab, basePos, Quaternion.identity, ParentPanel.transform);
            mScoreStars.Add(obj);
        }
    }

    private void CheckFinish()
    {
        if (!InGameManager.Inst.IsIdle)
            return;

        if (TargetValue.text == "0")
        {
            InGameManager.Inst.FinishGame(true);
            Hide();
        }

        if (Limit.text == "0")
        {
            InGameManager.Inst.FinishGame(false);
            Hide();
        }
    }
    private IEnumerator ScoreBarEffect(int prevScore, int addedScore)
    {
        int scorePerBar = UserSetting.ScorePerBar;
        int nextScore = prevScore + addedScore;
        float totalWidth = ScoreBar1.sprite.rect.width;
        float fromRate = (prevScore % scorePerBar) / (float)scorePerBar;
        float toRate = (nextScore % scorePerBar) / (float)scorePerBar;
        float bar2Width = totalWidth * (toRate - fromRate) + 1;
        ScoreBar1.fillAmount = fromRate;
        ScoreBar2.gameObject.SetActive(true);
        RectTransform rt = ScoreBar2.GetComponent<RectTransform>();
        Vector2 pos = rt.anchoredPosition;
        Vector2 size = rt.sizeDelta;
        pos.x = totalWidth * toRate;
        size.x = bar2Width;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        float time = 0;
        float duration = 0.5f;
        float slope1 = (toRate - fromRate) / (duration * duration);
        float slope2 = -bar2Width / (duration * duration);
        while (time < duration)
        {
            size.x = slope2 * time * time + bar2Width;
            ScoreBar1.fillAmount = slope1 * time * time + fromRate;
            rt.sizeDelta = size;
            time += Time.deltaTime;
            yield return null;
        }

        ScoreBar1.fillAmount = toRate;
        ScoreBar2.gameObject.SetActive(false);

    }

    public static MenuInGame Inst()
    {
        if(mInst == null)
            mInst = GameObject.Find("UIGroup").transform.Find(UIObjName).gameObject.GetComponent<MenuInGame>();
        return mInst;
    }
    public static void PopUp(StageInfo info)
    {
        Inst().InitUIState(info);
        Inst().gameObject.SetActive(true);
    }
    public static void Hide()
    {
        Inst().gameObject.SetActive(false);
    }
    private void InitUIState(StageInfo info)
    {
        foreach (GameObject obj in mScoreStars)
            Destroy(obj);

        mScoreStars.Clear();

        mStageInfo = info;
        ScoreBar1.fillAmount = 0;
        ScoreBar2.gameObject.SetActive(false);
        Lock.gameObject.SetActive(false);
        UnLock.gameObject.SetActive(true);


        mAddedScore = 0;
        mCurrentScore = 0;
        CurrentScore.text = "0";
        Limit.text = info.MoveLimit.ToString();
        TargetType.sprite = info.GoalTypeImage;
        TargetValue.text = info.GoalValue.ToString();
        KeepCombo.text = "0";
        CurrentComboDisplay.text = "0";
        StageLevel.text = info.Num.ToString();
        ComboNumber.gameObject.SetActive(false);

        //GameField.GetComponent<InGameManager>().EventOnChange = UpdatePanel;
    }

    public void AddScore(Product product)
    {
        mAddedScore += product.Combo;
        //GameObject comboTextObj = GameObject.Instantiate(ComboText, product.transform.position, Quaternion.identity, ParentPanel.transform);
        //Text combo = comboTextObj.GetComponent<Text>();
        //combo.text = product.Combo.ToString();
        //StartCoroutine(ComboEffect(comboTextObj));
    }
    IEnumerator ComboEffect(GameObject obj)
    {
        float time = 0;
        while(time < 0.7)
        {
            float x = (time * 10) + 1;
            float y = (1 / x) * Time.deltaTime;
            Vector3 pos = obj.transform.position;
            pos.y += y;
            obj.transform.position = pos;
            time += Time.deltaTime;
            yield return null;
        }
        Destroy(obj);
    }

    public int StarCount { get { return mScoreStars.Count; } }

    public int CurrentCombo
    {
        get
        {
            //return int.Parse(CurrentComboDisplay.text);
            return ComboNumber.GetNumber();
        }
        set
        {
            //CurrentComboDisplay.text = value.ToString();
            //CurrentComboDisplay.GetComponent<Animation>().Play("touch");
            ComboNumber.SetNumber(value);
        }
    }

    public int NextCombo
    {
        get { return int.Parse(KeepCombo.text); }
        set
        {
            int pre = int.Parse(KeepCombo.text);
            if (value > pre)
            {
                KeepCombo.text = value.ToString();
                KeepCombo.GetComponent<Animation>().Play("touch");
            }
            else if(value == 0)
            {
                KeepCombo.text = "0";
                KeepCombo.GetComponent<Animation>().Play("touch");
            }
        }
    }

    public void KeepNextCombo(Product product)
    {
        if (product.mSkill != ProductSkill.KeepCombo)
            return;

        int nextCombo = product.Combo;
        GameObject obj = GameObject.Instantiate(ItemPrefab, product.transform.position, Quaternion.identity, ParentPanel.transform);
        Image img = obj.GetComponent<Image>();
        img.sprite = product.Renderer.sprite;
        StartCoroutine(AnimateItem(obj, KeepCombo.transform.position, () =>
        {
            NextCombo = nextCombo;
        }));
    }

    public void OneMoreCombo(Product product)
    {
        if (product.mSkill != ProductSkill.OneMore)
            return;

        GameObject obj = GameObject.Instantiate(ItemPrefab, product.transform.position, Quaternion.identity, ParentPanel.transform);
        Image img = obj.GetComponent<Image>();
        img.sprite = product.Renderer.sprite;
        StartCoroutine(AnimateItem(obj, CurrentComboDisplay.transform.position, () =>
        {
            CurrentCombo++;
        }));
    }

    public void ReduceLimit()
    {
        int value = int.Parse(Limit.text) - 1;
        value = Mathf.Max(0, value);
        Limit.text = value.ToString();
        Limit.GetComponent<Animation>().Play("touch");
    }

    public void ReduceGoalValue(Vector3 worldPos, StageGoalType type)
    {
        if (type != mStageInfo.GoalTypeEnum)
            return;

        GameObject GoalTypeObj = GameObject.Instantiate(ItemPrefab, worldPos, Quaternion.identity, ParentPanel.transform);
        Image img = GoalTypeObj.GetComponent<Image>();
        img.sprite = mStageInfo.GoalTypeImage;
        StartCoroutine(AnimateItem(GoalTypeObj, TargetValue.transform.position, () =>
        {
            int value = int.Parse(TargetValue.text) - 1;
            value = Mathf.Max(0, value);
            TargetValue.text = value.ToString();
            TargetValue.GetComponent<Animation>().Play("touch");
        }));
    }
    IEnumerator AnimateItem(GameObject obj, Vector3 worldDest, Action action)
    {
        float duration = 1.0f;
        float time = 0;
        Vector3 startPos = obj.transform.position;
        Vector3 destPos = worldDest;
        Vector3 dir = destPos - startPos;
        Vector3 offset = Vector3.zero;
        Vector3 axisZ = new Vector3(0, 0, 1);
        Vector3 deltaSize = new Vector3(0.01f, 0.01f, 0);
        float slope = -dir.y / (duration * duration);
        while (time < duration)
        {
            offset.y = slope * (time - duration) * (time - duration) + dir.y;
            offset.x = dir.x * time;
            obj.transform.position = startPos + offset;
            //obj.transform.localScale += time < duration * 0.5f ? deltaSize : -deltaSize;
            obj.transform.Rotate(axisZ, offset.x - dir.x);
            time += Time.deltaTime;
            yield return null;
        }

        action.Invoke();
        Destroy(obj);
    }
    IEnumerator SkillMatchedEffect(GameObject obj)
    {
        float duration = Random.Range(0.4f, 0.5f);
        float height = 1.2f;
        float a = -1 * height / (duration * duration);
        Vector3 startPos = obj.transform.position;
        Vector3 offset = Vector3.zero;
        float dx = 1;
        float time = 0;
        while (time < duration)
        {
            offset.y = a * (time - duration) * (time - duration) + height;
            offset.x = dx * time;
            obj.transform.position = startPos + offset;
            time += Time.deltaTime;
            yield return null;
        }

        duration = Random.Range(0.2f, 0.3f);
        time = 0;
        startPos = obj.transform.position;
        Vector3 destPos = TargetValue.transform.position;
        Vector3 dir = destPos - startPos;
        float slope = dir.magnitude / (duration * duration);
        dir.Normalize();
        while (time < duration)
        {
            float dist = slope * time * time;
            obj.transform.position = startPos + (dir * dist);
            time += Time.deltaTime;
            yield return null;
        }

        int value = int.Parse(TargetValue.text) - 1;
        value = Mathf.Max(0, value);
        TargetValue.text = value.ToString();
        TargetValue.GetComponent<Animation>().Play("touch");
        Destroy(obj);
    }
    IEnumerator AnimateJump(GameObject obj)
    {
        float duration = 0.3f;
        float peekTime = duration * 0.5f;
        float peekHeight = 1.0f;
        float coeffB = 2f * peekHeight / peekTime;
        float coeffALow = -1f * peekHeight * 0.8f / (peekTime * 0.8f * peekTime * 0.8f);
        float coeffAHigh = -1f * peekHeight * 1.2f / (peekTime * 1.2f * peekTime * 1.2f);
        float coeffA = Random.Range(coeffALow, coeffAHigh);
        int dx = Random.Range(0, 1) == 0 ? 1 : -1;
        Vector3 startPos = obj.transform.position;
        Vector3 offset = Vector3.zero;
        Vector3 deltaSize = new Vector3(0.02f, 0.02f, 0.02f);
        float time = 0;
        while (time < peekTime)
        {
            offset.y = coeffA * time * time + coeffB * time;
            offset.x = dx * time;
            obj.transform.position = startPos + offset;
            obj.transform.localScale += time < peekTime ? deltaSize : -deltaSize;
            time += Time.deltaTime;
            yield return null;
        }

    }
    

    public void OnPause()
    {
        MenuPause.PopUp();
    }
    public void OnLockMatch(bool enableLock)
    {
        GameField.GetComponent<InGameManager>().MatchLock = enableLock;
        Lock.gameObject.SetActive(enableLock);
        UnLock.gameObject.SetActive(!enableLock);
    }
}
