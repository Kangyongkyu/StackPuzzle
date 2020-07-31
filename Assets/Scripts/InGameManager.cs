﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InGameManager : MonoBehaviour
{
    private static InGameManager mInst = null;
    public static InGameManager Inst
    {
        get
        {
            if (mInst == null)
                mInst = GameObject.Find("WorldSpace").transform.Find("GameField").GetComponent<InGameManager>();
            return mInst;
        }
    }

    public const int MatchCount = 3;
    public const float SwipeDetectRange = 0.1f;
    public const int GridSize = 1;
    public GameObject[] ProductPrefabs;
    public GameObject FramePrefab;

    private Frame[,] mFrames = null;
    private Product mDownProduct = null;
    private Vector3 mDownPosition;
    private StageInfo mStageInfo;
    private bool mIsRunning;

    public GameObject GameField;

    public Action<int, int> EventOnChange;
    public Action<bool> EventOnFinish;

    void Update()
    {
        if (!mIsRunning)
            return;

        if (IsFinished())
            return;

        CheckSwipe();
    }

    public void StartGame(StageInfo info)
    {
        ResetGame();

        gameObject.SetActive(true);
        mIsRunning = true;
        mStageInfo = info;
        CurrentScore = 0;
        RemainLimit = info.MoveLimit;

        mFrames = new Frame[info.RowCount, info.ColumnCount];
        for (int y = 0; y < info.ColumnCount; y++)
        {
            for (int x = 0; x < info.RowCount; x++)
            {
                GameObject frameObj = GameObject.Instantiate(FramePrefab, GameField.transform, false);
                frameObj.transform.localPosition = new Vector3(GridSize * x, GridSize * y, 0);
                mFrames[x, y] = frameObj.GetComponent<Frame>();
                mFrames[x, y].Initialize(x, y, this);
                CreateNewProduct(mFrames[x, y]);
            }
        }
    }
    public void PauseGame()
    {
        mIsRunning = false;
    }
    public void ResumeGame()
    {
        mIsRunning = true;
    }
    public void FinishGame(bool success)
    {
        ResetGame();
        EventOnFinish?.Invoke(success);
        gameObject.SetActive(false);
    }
    public void ResetGame()
    {
        int cnt = GameField.transform.childCount;
        for (int i = 0; i < cnt; ++i)
            Destroy(GameField.transform.GetChild(i).gameObject);

        mFrames = null;
        mDownProduct = null;
        mDownPosition = Vector3.zero;
        mStageInfo = null;
        EventOnChange = null;
        mIsRunning = false;
        CurrentScore = 0;
        RemainLimit = 0;
    }
    public int XCount { get { return mStageInfo.RowCount; } }
    public int YCount { get { return mStageInfo.ColumnCount; } }
    public int CurrentScore { get; set; }
    public int RemainLimit { get; set; }

    public Frame GetFrame(int x, int y)
    {
        return mFrames[x, y];
    }
    public void TakeScore(int score)
    {
        CurrentScore += score;
        EventOnChange?.Invoke(CurrentScore, RemainLimit);
    }
    void RemoveLimit()
    {
        RemainLimit--;
        EventOnChange?.Invoke(CurrentScore, RemainLimit);
    }
    public void CreateNewProduct(Frame parent)
    {
        int typeIdx = UnityEngine.Random.Range(0, ProductPrefabs.Length);
        GameObject obj = GameObject.Instantiate(ProductPrefabs[typeIdx], parent.transform, false);
        obj.transform.localPosition = new Vector3(0, 0, -1);
        obj.GetComponent<Product>().SetParentFrame(parent);
    }
    void CheckSwipe()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 worldPt = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Collider2D hit = Physics2D.OverlapPoint(worldPt);
            if (hit != null)
            {
                mDownProduct = hit.gameObject.GetComponent<Product>();
                mDownPosition = worldPt;
            }
        }
        else if (Input.GetMouseButton(0))
        {
            if(mDownProduct != null)
            {
                Vector3 curWorldPt = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                if ((curWorldPt - mDownPosition).magnitude >= SwipeDetectRange)
                {
                    Vector2 _currentSwipe = new Vector2(curWorldPt.x - mDownPosition.x, curWorldPt.y - mDownPosition.y);
                    _currentSwipe.Normalize();

                    Product target = null;
                    if (_currentSwipe.y > 0 && _currentSwipe.x > -0.5f && _currentSwipe.x < 0.5f)
                        target = mDownProduct.Up();
                    if (_currentSwipe.y < 0 && _currentSwipe.x > -0.5f && _currentSwipe.x < 0.5f)
                        target = mDownProduct.Down();
                    if (_currentSwipe.x < 0 && _currentSwipe.y > -0.5f && _currentSwipe.y < 0.5f)
                        target = mDownProduct.Left();
                    if (_currentSwipe.x > 0 && _currentSwipe.y > -0.5f && _currentSwipe.y < 0.5f)
                        target = mDownProduct.Right();

                    if (target != null && !mDownProduct.IsLocked() && !target.IsLocked())
                    {
                        RemoveLimit();
                        mDownProduct.StartSwipe(target.GetComponentInParent<Frame>());
                        target.StartSwipe(mDownProduct.GetComponentInParent<Frame>());
                    }

                    mDownProduct = null;
                }
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            mDownProduct = null;
        }
    }
    public int GetStarCount()
    {
        float rate = (float)CurrentScore / (float)mStageInfo.GoalScore;
        if (rate < 0.3f)
            return 0;
        else if (rate < 0.6f)
            return 1;
        else if (rate < 0.9f)
            return 2;
        return 3;
    }

    bool IsFinished()
    {
        if (CurrentScore >= mStageInfo.GoalScore)
        {
            Stage currentStage = StageManager.Inst.GetStage(mStageInfo.Num);
            currentStage.UpdateStarCount(GetStarCount());

            Stage nextStage = StageManager.Inst.GetStage(mStageInfo.Num + 1);
            if(nextStage != null)
                nextStage.UnLock();
            
            MenuComplete.PopUp(mStageInfo.Num, GetStarCount(), CurrentScore);

            FinishGame(true);
            return false;
        }
        else if(RemainLimit <= 0)
        {
            MenuFailed.PopUp(mStageInfo.Num, mStageInfo.GoalScore, CurrentScore);
            FinishGame(false);
            return false;
        }
        return true;
    }

}
