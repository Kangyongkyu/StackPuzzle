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
                mInst = GameObject.Find("WorldSpace").transform.Find("GameScreen/GameField").GetComponent<InGameManager>();
            return mInst;
        }
    }

    public GameObject[] ProductPrefabs;
    public GameObject FramePrefab1;
    public GameObject FramePrefab2;
    public GameObject MaskPrefab;

    private Frame[,] mFrames = null;
    private StageInfo mStageInfo = null;

    private int mCurrentScore = 0;
    private int mRemainLimit = 0;
    private int mKeepCombo = 0;
    private ProductColor mSkipColor = ProductColor.None;
    private Dictionary<int,List<Frame>> mDestroyes = new Dictionary<int, List<Frame>>();

    public int CountX { get { return mStageInfo.XCount; } }
    public int CountY { get { return mStageInfo.YCount; } }
    public Frame[,] Frames { get { return mFrames; } }
    public bool MatchLock { get; set; }
    public bool Pause { get; set; }
    public Action<int, int, Product> EventOnChange;

    public void StartGame(StageInfo info)
    {
        ResetGame();

        transform.parent.gameObject.SetActive(true);
        Pause = false;
        mStageInfo = info;
        mCurrentScore = 0;
        mRemainLimit = info.MoveLimit;
        SoundPlayer.Inst.PlayBackMusic(SoundPlayer.Inst.BackMusicInGame);

        GameObject mask = Instantiate(MaskPrefab, transform);
        mask.transform.localScale = new Vector3(info.XCount * 0.97f, info.YCount * 0.97f, 1);

        GetComponent<SwipeDetector>().EventSwipe = OnSwipe;

        StartCoroutine(CheckFinish());
        StartCoroutine(CreateNextProducts());

        float gridSize = UserSetting.GridSize;
        Vector3 localBasePos = new Vector3(-gridSize * info.XCount * 0.5f, -gridSize * info.YCount * 0.5f, 0);
        localBasePos.x += gridSize * 0.5f;
        localBasePos.y += gridSize * 0.5f;
        Vector3 localFramePos = new Vector3(0, 0, 0);
        mFrames = new Frame[info.XCount, info.YCount];
        for (int y = 0; y < info.YCount; y++)
        {
            for (int x = 0; x < info.XCount; x++)
            {
                GameObject frameObj = GameObject.Instantiate((x + y) % 2 == 0 ? FramePrefab1 : FramePrefab2, transform, false);
                localFramePos.x = gridSize * x;
                localFramePos.y = gridSize * y;
                frameObj.transform.localPosition = localBasePos + localFramePos;
                mFrames[x, y] = frameObj.GetComponent<Frame>();
                mFrames[x, y].Initialize(x, y, info.GetCell(x, y).FrameCoverCount);
                mFrames[x, y].GetFrame = GetFrame;
                Product pro = CreateNewProduct(mFrames[x, y]);
                pro.WrapChocoBlock(!info.GetCell(x, y).ProductMovable);
            }
        }
    }
    public void FinishGame(bool success)
    {
        ResetGame();
        transform.parent.gameObject.SetActive(false);
    }
    public bool IsIdle()
    {
        int count = 0;
        foreach (Frame frame in mFrames)
            if (frame.ChildProduct != null && !frame.ChildProduct.IsLocked())
                count++;
        return mFrames.Length == count;
    }

    public void OnSwipe(GameObject obj, SwipeDirection dir)
    {
        if (!IsSwapable())
            return;

        Product product = obj.GetComponent<Product>();
        Product targetProduct = null;
        switch (dir)
        {
            case SwipeDirection.UP: targetProduct = product.Up(); break;
            case SwipeDirection.DOWN: targetProduct = product.Down(); break;
            case SwipeDirection.LEFT: targetProduct = product.Left(); break;
            case SwipeDirection.RIGHT: targetProduct = product.Right(); break;
        }

        if (targetProduct != null && !product.IsLocked() && !targetProduct.IsLocked() && !product.IsChocoBlock() && !targetProduct.IsChocoBlock())
        {
            RemoveLimit();
            product.StartSwipe(targetProduct.GetComponentInParent<Frame>(), mKeepCombo);
            targetProduct.StartSwipe(product.GetComponentInParent<Frame>(), mKeepCombo);
            mKeepCombo = 0;
        }
    }
    private void OnMatch(List<Product> matches)
    {
        if (MatchLock)
            return;

        Product mainProduct = matches[0];
        mainProduct.BackupSkillToFrame(matches.Count, mSkipColor != ProductColor.None);

        List<Product> allSameColors = ApplySkillEffects(matches);

        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectMatched);

        List<Product> destroies = allSameColors.Count > 0 ? allSameColors : matches;
        int currentCombo = mainProduct.Combo;
        foreach (Product pro in destroies)
        {
            pro.Combo = currentCombo + 1;
            pro.StartDestroy();
            AddScore(pro);
        }

        mainProduct.StartFlash(matches);
    }
    private void OnDestroyProduct(Product pro)
    {
        int idxX = pro.ParentFrame.IndexX;
        if (!mDestroyes.ContainsKey(idxX))
            mDestroyes[idxX] = new List<Frame>();
        mDestroyes[idxX].Add(pro.ParentFrame);
    }

    private IEnumerator CreateNextProducts()
    {
        while(true)
        {
            yield return new WaitForSeconds(0.1f);

            foreach (var vert in mDestroyes)
            {
                int idxX = vert.Key;
                List<Frame> vertFrames = vert.Value;
                vertFrames.Sort((a, b) => { return a.IndexY - b.IndexY; });

                Queue<ProductSkill> nextSkills = new Queue<ProductSkill>();

                Frame curFrame = vertFrames[0];
                Frame validFrame = curFrame;
                int emptyCount = 0;
                while (curFrame != null)
                {
                    Product pro = NextUpProductFrom(validFrame);
                    if (pro == null)
                    {
                        validFrame = null;
                        if (emptyCount == 0)
                            emptyCount = mStageInfo.YCount - curFrame.IndexY;
                        pro = CreateNewProduct(curFrame, nextSkills.Count > 0 ? nextSkills.Dequeue() : ProductSkill.Nothing);
                        pro.StartDropAnimate(curFrame, emptyCount, curFrame == vertFrames[0]);
                    }
                    else
                    {
                        validFrame = pro.ParentFrame;
                        pro.StartDropAnimate(curFrame, pro.ParentFrame.IndexY - curFrame.IndexY, curFrame == vertFrames[0]);
                        if(curFrame.SkillBackupSpace != ProductSkill.Nothing)
                        {
                            nextSkills.Enqueue(curFrame.SkillBackupSpace);
                            curFrame.SkillBackupSpace = ProductSkill.Nothing;
                        }
                    }

                    curFrame = curFrame.Up();
                }
            }

            mDestroyes.Clear();
        }
    }
    private Product CreateNewProduct(Frame parent, ProductSkill skill = ProductSkill.Nothing)
    {
        int colorCount = Math.Min(mStageInfo.ColorCount, ProductPrefabs.Length);
        int typeIdx = UnityEngine.Random.Range(0, colorCount);
        if (mSkipColor != ProductColor.None && ProductPrefabs[typeIdx].GetComponent<Product>().mColor == mSkipColor)
        {
            int nextIdx = UnityEngine.Random.Range(1, colorCount);
            typeIdx = (typeIdx + nextIdx) % colorCount;
        }
        GameObject obj = GameObject.Instantiate(ProductPrefabs[typeIdx], parent.transform, false);
        Product product = obj.GetComponent<Product>();
        product.transform.localPosition = new Vector3(0, 0, -1);
        product.ParentFrame = parent;
        product.EventMatched = OnMatch;
        product.EventDestroyed = OnDestroyProduct;
        product.ChangeSkilledProduct(skill);
        return product;
    }
    private List<Product> ApplySkillEffects(List<Product> matches)
    {
        int skillComboCount = 0;
        bool keepCombo = false;
        List<Product> allSameColors = new List<Product>();
        foreach (Product pro in matches)
        {
            if (pro.mSkill == ProductSkill.MatchOneMore)
                skillComboCount++;
            else if (pro.mSkill == ProductSkill.KeepCombo)
                keepCombo = true;
            else if (pro.mSkill == ProductSkill.BreakSameColor && allSameColors.Count == 0)
            {
                foreach(Frame frame in mFrames)
                    if (frame.ChildProduct != null && frame.ChildProduct.mColor == matches[0].mColor)
                        allSameColors.Add(frame.ChildProduct);
            }
            else if (pro.mSkill == ProductSkill.ReduceColor && allSameColors.Count == 0)
            {
                SetSkipProduct(matches[0].mColor, 5);
                foreach (Frame frame in mFrames)
                    if (frame.ChildProduct != null && frame.ChildProduct.mColor == matches[0].mColor)
                        allSameColors.Add(frame.ChildProduct);
            }
        }

        if (skillComboCount > 0)
            foreach (Product pro in matches)
                pro.Combo += skillComboCount;

        if (keepCombo)
            mKeepCombo = Math.Max(mKeepCombo, matches[0].Combo);

        return allSameColors;
    }
    private IEnumerator CheckFinish()
    {
        float time = 0;
        while (time < 3)
        {
            if (IsSwapable())
                yield return new WaitForSeconds(1);
            else
            {
                time = (mDestroyes.Count > 0) ? 0 : (time + Time.deltaTime);
                yield return null;
            }
        }
        FinishGame();
    }
    private void FinishGame()
    {
        if (mCurrentScore >= mStageInfo.GoalScore)
        {
            int starCount = GetStarCount(mCurrentScore, mStageInfo.GoalScore);
            Stage currentStage = StageManager.Inst.GetStage(mStageInfo.Num);
            currentStage.UpdateStarCount(starCount);

            Stage nextStage = StageManager.Inst.GetStage(mStageInfo.Num + 1);
            if (nextStage != null)
                nextStage.UnLock();

            SoundPlayer.Inst.Player.Stop();
            MenuComplete.PopUp(mStageInfo.Num, starCount, mCurrentScore);

            FinishGame(true);
        }
        else if (mRemainLimit <= 0)
        {
            SoundPlayer.Inst.Player.Stop();
            MenuFailed.PopUp(mStageInfo.Num, mStageInfo.GoalScore, mCurrentScore);

            FinishGame(false);
        }
    }

    private void ResetGame()
    {
        int cnt = transform.childCount;
        for (int i = 0; i < cnt; ++i)
            Destroy(transform.GetChild(i).gameObject);

        mFrames = null;
        mStageInfo = null;
        Pause = false;
        mCurrentScore = 0;
        mRemainLimit = 0;
        mKeepCombo = 0;
        mSkipColor = ProductColor.None;
        MatchLock = false;
    }
    private Product NextUpProductFrom(Frame frame)
    {
        Frame curFrame = frame;
        while (curFrame != null)
        {
            if (curFrame.ChildProduct != null)
                return curFrame.ChildProduct;

            curFrame = curFrame.Up();
        }
        return null;
    }
    public Frame GetFrame(int x, int y)
    {
        if (x < 0 || x >= mStageInfo.XCount || y < 0 || y >= mStageInfo.YCount)
            return null;
        return mFrames[x, y];
    }
    private void AddScore(Product product)
    {
        mCurrentScore += (UserSetting.scorePerProduct * product.Combo);
        EventOnChange?.Invoke(0, mCurrentScore, product);
    }
    private void SetSkipProduct(ProductColor color, int returnCount)
    {
        if (mSkipColor != ProductColor.None)
            return;

        mSkipColor = color;
        StartCoroutine(ReturnToStopSkipping(returnCount));
    }
    private IEnumerator ReturnToStopSkipping(int count)
    {
        int returnCount = mRemainLimit - count;
        if (returnCount < 0) returnCount = 0;
        while (returnCount < mRemainLimit)
            yield return null;

        mSkipColor = ProductColor.None;
    }
    private void RemoveLimit()
    {
        mRemainLimit--;
        EventOnChange?.Invoke(mRemainLimit, 0, null);
    }
    private bool IsSwapable()
    {
        if (Pause)
            return false;

        if (mCurrentScore >= mStageInfo.GoalScore)
            return false;

        if (mRemainLimit <= 0)
            return false;

        return true;
    }

    public static int GetStarCount(int score, int target)
    {
        return score / target;
    }

}
