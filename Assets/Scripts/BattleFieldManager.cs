﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleFieldManager : MonoBehaviour
{
    public const int MatchCount = 3;
    public const int attackScore = 5;
    public const float GridSize = 0.8f;
    public const int NextRequestCount = 500;

    public GameObject[] ProductPrefabs;
    public GameObject FramePrefab1;
    public GameObject FramePrefab2;
    public GameObject MaskPrefab;
    public BattleFieldManager Opponent;

    private Frame[,] mFrames = null;
    private Dictionary<int, List<Frame>> mDestroyes = new Dictionary<int, List<Frame>>();
    private List<ProductColor> mNextColors = new List<ProductColor>();
    private int mNextPositionIndex = 0;
    private int mThisUserPK = 0;
    private int mKeepCombo = 0;
    private int mCountX = 0;
    private int mCountY = 0;

    public bool MatchLock { get; set; }

    public void StartGame(int userPK, int XCount, int YCount, ProductColor[,] initColors)
    {
        ResetGame();
    
        mThisUserPK = userPK;
        mCountX = XCount;
        mCountY = YCount;

        transform.parent.gameObject.SetActive(true);
        SoundPlayer.Inst.PlayBackMusic(SoundPlayer.Inst.BackMusicInGame);
    
        GameObject mask = Instantiate(MaskPrefab, transform);
        mask.transform.localScale = new Vector3(XCount * 0.97f, YCount * 0.97f, 1);

        RegisterSwipeEvent();

        RequestNextColors(NextRequestCount);

        StartCoroutine(CheckFinish());
        StartCoroutine(CreateNextProducts());
    
        Vector3 localBasePos = new Vector3(-GridSize * XCount * 0.5f, -GridSize * YCount * 0.5f, 0);
        localBasePos.x += GridSize * 0.5f;
        localBasePos.y += GridSize * 0.5f;
        Vector3 localFramePos = new Vector3(0, 0, 0);
        mFrames = new Frame[XCount, YCount];
        for (int y = 0; y < YCount; y++)
        {
            for (int x = 0; x < XCount; x++)
            {
                GameObject frameObj = GameObject.Instantiate((x + y) % 2 == 0 ? FramePrefab1 : FramePrefab2, transform, false);
                localFramePos.x = GridSize * x;
                localFramePos.y = GridSize * y;
                frameObj.transform.localPosition = localBasePos + localFramePos;
                mFrames[x, y] = frameObj.GetComponent<Frame>();
                mFrames[x, y].Initialize(x, y, 0);
                mFrames[x, y].GetFrame = GetFrame;
                CreateNewProduct(mFrames[x, y], initColors[x,y]);
            }
        }

    }
    public void FinishGame(bool success)
    {
        ResetGame();
        transform.parent.gameObject.SetActive(false);

        EndGame info = new EndGame();
        info.userPk = mThisUserPK;
        info.win = success;
        NetClientApp.GetInstance().Request(NetCMD.EndGame, info, null);
    }

    private void OnSwipe(GameObject obj, SwipeDirection dir)
    {
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
            SendSwipeInfo(product.ParentFrame.IndexX, product.ParentFrame.IndexY);
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
        mainProduct.BackupSkillToFrame(matches.Count, true);

        List<Product> allSameColors = ApplySkillEffects(matches);

        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectMatched);

        List<Product> destroies = allSameColors.Count > 0 ? allSameColors : matches;
        int currentCombo = mainProduct.Combo;
        foreach (Product pro in destroies)
        {
            pro.Combo = currentCombo + 1;
            pro.StartDestroy();
            Attack(pro);
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
        while (true)
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
                            emptyCount = mCountY - curFrame.IndexY;
                        pro = CreateNewProduct(curFrame, GetNextColor(), nextSkills.Count > 0 ? nextSkills.Dequeue() : ProductSkill.Nothing);
                        pro.StartDropAnimate(curFrame, emptyCount, curFrame == vertFrames[0]);
                    }
                    else
                    {
                        validFrame = pro.ParentFrame;
                        pro.StartDropAnimate(curFrame, pro.ParentFrame.IndexY - curFrame.IndexY, curFrame == vertFrames[0]);
                        if (curFrame.SkillBackupSpace != ProductSkill.Nothing)
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
    private Product CreateNewProduct(Frame parent, ProductColor color,ProductSkill skill = ProductSkill.Nothing)
    {
        int typeIdx = (int)color;
        GameObject obj = GameObject.Instantiate(ProductPrefabs[typeIdx], parent.transform, false);
        Product product = obj.GetComponent<Product>();
        product.transform.localPosition = new Vector3(0, 0, -1);
        product.ParentFrame = parent;
        product.EventMatched = OnMatch;
        product.EventDestroyed = OnDestroyProduct;
        product.ChangeSkilledProduct(skill);
        if (!IsPlayerField())
            obj.GetComponent<BoxCollider2D>().enabled = false;
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
        while (true)
        {
            yield return new WaitForSeconds(1);

            bool isFinished = true;
            Dictionary<ProductColor, int> colorCount = new Dictionary<ProductColor, int>();
            foreach(Frame frame in mFrames)
            {
                Product pro = frame.ChildProduct;
                if (pro == null || pro.IsChocoBlock())
                    continue;

                if (colorCount.ContainsKey(pro.mColor))
                    colorCount[pro.mColor] = 1;
                else
                    colorCount[pro.mColor] += 1;

                if(colorCount[pro.mColor] >= MatchCount)
                {
                    isFinished = false;
                    break;
                }
            }

            if (isFinished)
                break;
        }

        if(IsPlayerField())
            FinishGame(false);
        else
            FinishGame(true);
    }


    private void SendSwipeInfo(int idxX, int idxY)
    {
        SwipeInfo info = new SwipeInfo();
        info.idxX = idxX;
        info.idxY = idxY;
        info.matchable = !MatchLock;
        info.userPk = mThisUserPK;
        NetClientApp.GetInstance().Request(NetCMD.SendSwipe, info, null);
    }
    private void RegisterSwipeEvent()
    {
        if (IsPlayerField())
        {
            GetComponent<SwipeDetector>().EventSwipe = OnSwipe;
        }
        else
        {
            GetComponent<SwipeDetector>().enabled = false;
            NetClientApp.GetInstance().WaitResponse(NetCMD.SendSwipe, (object response) =>
            {
                SwipeInfo res = response as SwipeInfo;
                if (res.userPk == mThisUserPK)
                    return;

                MatchLock = res.matchable;
                Product pro = mFrames[res.idxX, res.idxY].ChildProduct;
                OnSwipe(pro.gameObject, res.dir);
            });
        }
    }
    private bool IsPlayerField()
    {
        return UserSetting.UserPK != mThisUserPK;
    }
    private void ResetGame()
    {
        int cnt = transform.childCount;
        for (int i = 0; i < cnt; ++i)
            Destroy(transform.GetChild(i).gameObject);

        mDestroyes.Clear();
        mNextColors.Clear();

        mFrames = null;
        mNextPositionIndex = 0;
        MatchLock = false;
        mThisUserPK = 0;
        mKeepCombo = 0;
        mCountX = 0;
        mCountY = 0;
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
    private Frame GetFrame(int x, int y)
    {
        if (x < 0 || x >= mCountX || y < 0 || y >= mCountY)
            return null;
        return mFrames[x, y];
    }
    private void Attack(Product pro)
    {
        Debug.Log("Attack!!");
    }
    private ProductColor GetNextColor()
    {
        int remainCount = mNextColors.Count - mNextPositionIndex;
        if (remainCount < NextRequestCount / 3)
            RequestNextColors(NextRequestCount);

        ProductColor next = mNextColors[mNextPositionIndex];
        mNextPositionIndex++;
        return next;
    }
    private void RequestNextColors(int count)
    {
        NextProducts info = new NextProducts();
        info.userPk = mThisUserPK;
        info.offset = mNextColors.Count;
        info.requestCount = count;
        NetClientApp.GetInstance().Request(NetCMD.NextProducts, info, (object response) =>
        {
            NextProducts res = response as NextProducts;
            mNextColors.AddRange(res.nextProducts);
        });
    }

    //attack
}
