﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using SkillPair = System.Tuple<PVPCommand, UnityEngine.Sprite>;

public class VerticalFrames
{
    public Frame[] Frames;
    public List<Product> NewProducts = new List<Product>();
}

public enum GameFieldType { Noting, Stage, pvpPlayer, pvpOpponent }
public enum InGameState { Noting, Running, Paused, Win, Lose }
public class InGameManager : MonoBehaviour
{
    private static InGameManager mInstStage = null;
    private static InGameManager mInstPVP_Player = null;
    private static InGameManager mInstPVP_Opponent = null;
    public static InGameManager InstStage
    { get { if (mInstStage == null) mInstStage = GameObject.Find("WorldSpace").transform.Find("GameScreen/GameField").GetComponent<InGameManager>(); return mInstStage; } }
    public static InGameManager InstPVP_Player
    { get { if (mInstPVP_Player == null) mInstPVP_Player = GameObject.Find("WorldSpace").transform.Find("BattleScreen/GameFieldMe").GetComponent<InGameManager>(); return mInstPVP_Player; } }
    public static InGameManager InstPVP_Opponent
    { get { if (mInstPVP_Opponent == null) mInstPVP_Opponent = GameObject.Find("WorldSpace").transform.Find("BattleScreen/GameFieldOpp").GetComponent<InGameManager>(); return mInstPVP_Opponent; } }
    public static InGameManager InstCurrent
    { get { if (mInstStage != null && mInstStage.gameObject.activeSelf) return mInstStage; else return mInstPVP_Player; } }

    private const float durationDrop = 0.6f;
    private const float intervalMatch = 0.5f;
    private const float intervalDrop = 0.1f;
    private const float durationMerge = intervalMatch + intervalDrop;

    public GameObject[] ProductPrefabs;
    public GameObject FramePrefab1;
    public GameObject FramePrefab2;
    public GameObject MaskPrefab;
    public GameObject ComboNumPrefab;
    public GameObject AttackPointPrefab;

    public GameObject ExplosionParticle;
    public GameObject MergeParticle;
    public GameObject StripeParticle;
    public GameObject SparkParticle;
    public GameObject LaserParticle;
    public GameObject BombParticle;
    public GameObject ShieldParticle;
    public GameObject ScoreBuffParticle;
    public GameObject CloudPrefab;
    public GameObject UpsideDownParticle;
    public GameObject RemoveBadEffectParticle;

    public GameObject[] SkillSlots;

    private Frame[,] mFrames = null;
    private StageInfo mStageInfo = null;
    private UserInfo mUserInfo = null;
    private bool mMoveLock = false;
    private bool mIsCycling = false;
    private bool mIsSwipping = false;
    private DateTime mStartTime = DateTime.Now;
    private bool mRemoveBadEffectsCoolTime = false;
    private VerticalFrames[] mFrameDropGroup = null;

    private Queue<ProductSkill> mNextSkills = new Queue<ProductSkill>();
    private LinkedList<PVPInfo> mNetMessages = new LinkedList<PVPInfo>();


    public SkillPair[] SkillMapping = new SkillPair[7]
    {
        new SkillPair(PVPCommand.Undef, null),
        new SkillPair(PVPCommand.Undef, null),
        new SkillPair(PVPCommand.Undef, null),
        new SkillPair(PVPCommand.Undef, null),
        new SkillPair(PVPCommand.Undef, null),
        new SkillPair(PVPCommand.Undef, null),
        new SkillPair(PVPCommand.Undef, null)
    };
    public InGameBillboard Billboard = new InGameBillboard();
    public GameFieldType FieldType { get {
            return this == mInstStage ? GameFieldType.Stage :
                (this == mInstPVP_Player ? GameFieldType.pvpPlayer :
                (this == mInstPVP_Opponent ? GameFieldType.pvpOpponent : GameFieldType.Noting)); }
    }
    public Frame GetFrame(int x, int y)
    {
        if (x < 0 || x >= mStageInfo.XCount || y < 0 || y >= mStageInfo.YCount)
            return null;
        if (mFrames[x, y].Empty)
            return null;
        return mFrames[x, y];
    }
    public Frame CenterFrame { get { return mFrames[CountX / 2, CountY / 2]; } }
    public GameObject ShieldSlot { get { return SkillSlots[0]; } }
    public GameObject ScoreBuffSlot { get { return SkillSlots[1]; } }
    public GameObject UpsideDownSlot { get { return SkillSlots[2]; } }
    public bool IsIdle { get { return !mIsCycling && !mIsSwipping; } }
    public int CountX { get { return mStageInfo.XCount; } }
    public int CountY { get { return mStageInfo.YCount; } }
    public float ColorCount { get { return mStageInfo.ColorCount; } }
    public int UserPk { get { return mUserInfo.userPk; } }
    public AttackPoints AttackPoints { get; set; }
    public InGameManager Opponent { get { return FieldType == GameFieldType.pvpPlayer ? InstPVP_Opponent : InstPVP_Player; } }
    public InGameBillboard GetBillboard() { return Billboard; }
    public float GridSize { get { return UserSetting.GridSize * transform.localScale.x; } }
    public Rect FieldWorldRect    {
        get {
            Rect rect = new Rect(Vector2.zero, new Vector2(GridSize * CountX, GridSize * CountY));
            rect.center = transform.position;
            return rect;
        }
    }


    public Action<Vector3, StageGoalType> EventBreakTarget;
    public Action<Product[]> EventMatched;
    public Action<bool> EventFinish;
    public Action<int> EventCombo;
    public Action<int> EventRemainTime;
    public Action EventReduceLimit;

    private void Update()
    {
        if(!mIsCycling)
        {
            foreach (var group in mFrameDropGroup)
                StartToDropVerticalFrames(group);
        }
    }
    public void StartGame(StageInfo info, UserInfo userInfo)
    {
        ResetGame();
        Vector3 pos = transform.position;

        transform.parent.gameObject.SetActive(true);
        gameObject.SetActive(true);
        mStageInfo = info;
        mUserInfo = userInfo;
        mStartTime = DateTime.Now;

        if (FieldType == GameFieldType.Stage)
        {
            GetComponent<SwipeDetector>().EventSwipe = OnSwipe;
            GetComponent<SwipeDetector>().EventClick = OnClick;
            StartCoroutine(CheckFinishGame());
        }
        else if (FieldType == GameFieldType.pvpPlayer)
        {
            GetComponent<SwipeDetector>().EventSwipe = OnSwipe;
            GetComponent<SwipeDetector>().EventClick = OnClick;
            StartCoroutine(CheckFinishGame());
        }
        else if (FieldType == GameFieldType.pvpOpponent)
        {
            transform.localScale = new Vector3(UserSetting.BattleOppResize, UserSetting.BattleOppResize, 1);
            StartCoroutine(ProcessNetMessages());
        }


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
                mFrames[x, y].Initialize(this, x, y, info.GetCell(x, y).FrameCoverCount);
                mFrames[x, y].EventBreakCover = () => {
                    Billboard.CoverCount++;
                    EventBreakTarget?.Invoke(mFrames[x, y].transform.position, StageGoalType.Cover);
                };
            }
        }

        SecondaryInitFrames();
        InitDropGroupFrames();

        GameObject ap = Instantiate(AttackPointPrefab, transform);
        ap.transform.localPosition = localBasePos + new Vector3(-gridSize + 0.2f, gridSize * CountY - 0.1f, 0);
        AttackPoints = ap.GetComponent<AttackPoints>();
    }
    public void InitProducts()
    {
        List<Product> initProducts = new List<Product>();
        for (int y = 0; y < CountY; y++)
        {
            for (int x = 0; x < CountX; x++)
            {
                if (mFrames[x, y].Empty)
                    continue;

                Product pro = CreateNewProduct(mFrames[x, y]);
                pro.SetChocoBlock(mStageInfo.GetCell(x, y).ProductChocoCount);
                pro.EventUnWrapChoco = () => {
                    Billboard.ChocoCount++;
                    EventBreakTarget?.Invoke(pro.transform.position, StageGoalType.Choco);
                };
                initProducts.Add(pro);
            }
        }

        Network_StartGame(Serialize(initProducts.ToArray()));
    }
    public void FinishGame()
    {
        ResetGame();
        gameObject.SetActive(false);
        transform.parent.gameObject.SetActive(false);
    }

    public void OnClick(GameObject clickedObj)
    {
        if (!IsIdle || mMoveLock)
            return;

        Product pro = clickedObj.GetComponent<Product>();
        if(pro.mSkill != ProductSkill.Nothing)
        {
            pro.mAnimation.Play("swap");
            CreateSparkEffect(pro.transform.position);
            StartCoroutine(UnityUtils.CallAfterSeconds(0.4f, () => {
                StartCoroutine(LoopBreakSkill(new Product[1] { pro }, null));
            }));
        }
        else
        {
            List<Product[]> matches = FindMatchedProducts(new Product[1] { pro });
            if (matches.Count <= 0)
            {
                pro.mAnimation.Play("swap");
            }
            else
            {
                RemoveLimit();
                StartCoroutine(DoMatchingCycle(matches[0]));
            }
        }
    }
    public void OnSwipe(GameObject swipeObj, SwipeDirection dir)
    {
        if (mMoveLock)
            return;

        SwipeDirection fixedDir = dir;
        if (UpsideDownSlot.activeSelf)
        {
            switch (dir)
            {
                case SwipeDirection.UP: fixedDir = SwipeDirection.DOWN; break;
                case SwipeDirection.DOWN: fixedDir = SwipeDirection.UP; break;
                case SwipeDirection.LEFT: fixedDir = SwipeDirection.RIGHT; break;
                case SwipeDirection.RIGHT: fixedDir = SwipeDirection.LEFT; break;
            }
        }

        Product product = swipeObj.GetComponent<Product>();
        if (product.IsLocked() || product.IsIced)
            return;

        Product targetProduct = null;
        switch (fixedDir)
        {
            case SwipeDirection.UP: targetProduct = product.Up(); break;
            case SwipeDirection.DOWN: targetProduct = product.Down(); break;
            case SwipeDirection.LEFT: targetProduct = product.Left(); break;
            case SwipeDirection.RIGHT: targetProduct = product.Right(); break;
        }

        if (targetProduct == null || targetProduct.IsLocked() || targetProduct.IsIced)
            return;

        RemoveLimit();

        if (product.mSkill != ProductSkill.Nothing && targetProduct.mSkill != ProductSkill.Nothing)
        {
            CreateMergeEffect(product, targetProduct);
            product.SkillMerge(targetProduct, () => {
                SwipeSkilledProducts(product, targetProduct);
            });
        }
        else
        {
            mIsSwipping = true;
            Network_Swipe(product, dir);
            product.Swipe(targetProduct, () => {
                mIsSwipping = false;
            });
        }
    }

    #region Utility
    private IEnumerator DoMatchingCycle(Product[] firstMatches)
    {
        mIsCycling = true;
        Billboard.CurrentCombo = 1;
        EventCombo?.Invoke(Billboard.CurrentCombo);
        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectMatched);

        List<Frame> nextScanFrames = new List<Frame>();
        nextScanFrames.AddRange(ToFrames(firstMatches));
        ProductSkill nextSkill = CheckSkillable(firstMatches);
        if (nextSkill == ProductSkill.Nothing)
            DestroyProducts(firstMatches);
        else
            MergeProducts(firstMatches, nextSkill);

        while (true)
        {
            List<Product> aroundProducts = FindAroundProducts(nextScanFrames.ToArray());
            List<Product[]> nextMatches = FindMatchedProducts(aroundProducts.ToArray());
            if (nextMatches.Count <= 0)
                break;

            yield return new WaitForSeconds(intervalMatch);

            Billboard.CurrentCombo++;
            Billboard.MaxCombo = Math.Max(Billboard.CurrentCombo, Billboard.MaxCombo);
            Billboard.ComboCounter[Billboard.CurrentCombo]++;
            EventCombo?.Invoke(Billboard.CurrentCombo);
            SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectMatched);

            nextScanFrames.Clear();
            foreach (Product[] matches in nextMatches)
            {
                nextScanFrames.AddRange(ToFrames(matches));
                nextSkill = CheckSkillable(matches);
                if (nextSkill == ProductSkill.Nothing)
                    DestroyProducts(matches);
                else
                    MergeProducts(matches, nextSkill);
            }
        }

        yield return new WaitForSeconds(0.2f);

        EventCombo?.Invoke(0);
        mIsCycling = false;
    }
    private Product[] ToProducts(Frame[] frames)
    {
        List<Product> products = new List<Product>();
        foreach (Frame frame in frames)
            if(frame.ChildProduct != null)
                products.Add(frame.ChildProduct);
        return products.ToArray();
    }
    private Frame[] ToFrames(Product[] products)
    {
        List<Frame> frames = new List<Frame>();
        foreach (Product pro in products)
            frames.Add(pro.ParentFrame);
        return frames.ToArray();
    }
    private Product[] DestroyProducts(Product[] matches)
    {
        if (matches == null || matches.Length <= 0)
            return matches;

        List<ProductInfo> nextProducts = new List<ProductInfo>();
        List<Product> rets = new List<Product>();

        int addedScore = 0;
        foreach (Product pro in matches)
        {
            Frame curFrame = pro.StartDestroy(1.3f, 0.2f, Billboard.CurrentCombo);
            if (curFrame == null)
                continue;

            addedScore += Billboard.CurrentCombo;
            rets.Add(pro);

            Product newPro = InstanceNewProduct(curFrame);
            nextProducts.Add(new ProductInfo(newPro.mColor, curFrame.IndexX, curFrame.IndexY));
        }

        //if (FieldType == GameFieldType.Stage)
        //    ReduceTargetScoreCombo(mainProduct, Billboard.CurrentScore, Billboard.CurrentScore + addedScore);
        //else
        //    Attack(addedScore, mainProduct.transform.position);

        Billboard.CurrentScore += addedScore;
        Billboard.DestroyCount += matches.Length;

        Network_Destroy(nextProducts.ToArray(), ProductSkill.Nothing);
        EventMatched?.Invoke(matches);
        return rets.ToArray();
    }
    private void MergeProducts(Product[] matches, ProductSkill makeSkill)
    {
        List<ProductInfo> nextProducts = new List<ProductInfo>();
        Frame mainFrame = matches[0].ParentFrame;

        int addedScore = 0;
        foreach (Product pro in matches)
        {
            Frame curFrame = pro.ParentFrame;
            addedScore += Billboard.CurrentCombo;
            pro.Combo = Billboard.CurrentCombo;
            if (curFrame == mainFrame)
            {
                pro.StartToChangeSkilledProduct(0.2f, makeSkill);
                continue;
            }

            pro.StartMergeTo(matches[0], 0.2f);
            Product newPro = InstanceNewProduct(curFrame);
            nextProducts.Add(new ProductInfo(newPro.mColor, curFrame.IndexX, curFrame.IndexY));
        }

        //if (FieldType == GameFieldType.Stage)
        //    ReduceTargetScoreCombo(mainProduct, Billboard.CurrentScore, Billboard.CurrentScore + addedScore);
        //else
        //    Attack(addedScore, mainProduct.transform.position);

        Billboard.CurrentScore += addedScore;
        Billboard.DestroyCount += matches.Length;

        Network_Destroy(nextProducts.ToArray(), makeSkill);
        EventMatched?.Invoke(matches);
    }
    private Product InstanceNewProduct(Frame emptyFrame)
    {
        Frame[] verties = emptyFrame.VertFrames.Frames;
        Product pro = CreateNewProduct();

        Vector3 pos = verties[verties.Length - 1].transform.position;
        pro.transform.position = pos + new Vector3(0, GridSize, -1);

        emptyFrame.VertFrames.NewProducts.Add(pro);
        return pro;
    }
    private void StartToDropVerticalFrames(VerticalFrames group)
    {
        if (group.NewProducts.Count <= 0)
            return;

        foreach (Frame frame in group.Frames)
        {
            if (frame.ChildProduct == null || frame.ChildProduct.IsLocked())
                continue;

            Frame downFrame = frame.Down();
            if (downFrame == null || downFrame.ChildProduct != null)
                continue;

            frame.ChildProduct.Drop(null);
        }

        Product prvPro = null;
        foreach (Product newProduct in group.NewProducts)
        {
            if (prvPro == null)
            {
                if (!newProduct.IsDropping)
                    newProduct.Drop(() => { group.NewProducts.Remove(newProduct); });
            }
            else
            {
                if (!newProduct.IsDropping)
                    newProduct.Drop(prvPro, () => { group.NewProducts.Remove(newProduct); });
            }
            prvPro = newProduct;
        }
    }
    private Product[] ScanHorizenProducts(Product target, int range = 0)
    {
        if (target == null)
            return null;

        List<Product> rets = new List<Product>();
        Frame frameOf = target.ParentFrame != null ? target.ParentFrame : GetFrame(target.transform.position.x, target.transform.position.y);
        int idxY = frameOf.IndexY;
        for (int x = 0; x < CountX; ++x)
        {
            Product pro = mFrames[x, idxY].ChildProduct;
            if (pro != null && !pro.IsLocked() && !pro.IsChocoBlock() && !pro.IsIced)
                rets.Add(pro);
        }

        if (range >= 1 && frameOf.IndexY + 1 < CountY)
        {
            int idxYUp = frameOf.IndexY + 1;
            for (int x = 0; x < CountX; ++x)
            {
                Product pro = mFrames[x, idxYUp].ChildProduct;
                if (pro != null && !pro.IsLocked() && !pro.IsChocoBlock() && !pro.IsIced)
                    rets.Add(pro);
            }
        }

        if (range >= 2 && frameOf.IndexY - 1 >= 0)
        {
            int idxYDown = frameOf.IndexY - 1;
            for (int x = 0; x < CountX; ++x)
            {
                Product pro = mFrames[x, idxYDown].ChildProduct;
                if (pro != null && !pro.IsLocked() && !pro.IsChocoBlock() && !pro.IsIced)
                    rets.Add(pro);
            }
        }

        return rets.ToArray();
    }
    private Product[] ScanVerticalProducts(Product target, int range = 0)
    {
        if (target == null)
            return null;

        List<Product> rets = new List<Product>();
        Frame frameOf = target.ParentFrame != null ? target.ParentFrame : GetFrame(target.transform.position.x, target.transform.position.y);
        int idxX = frameOf.IndexX;
        for (int y = 0; y < CountY; ++y)
        {
            Product pro = mFrames[idxX, y].ChildProduct;
            if (pro != null && !pro.IsLocked() && !pro.IsChocoBlock() && !pro.IsIced)
                rets.Add(pro);
        }

        if (range >= 1 && frameOf.IndexX + 1 < CountX)
        {
            int idxXRight = frameOf.IndexX + 1;
            for (int y = 0; y < CountY; ++y)
            {
                Product pro = mFrames[idxXRight, y].ChildProduct;
                if (pro != null && !pro.IsLocked() && !pro.IsChocoBlock() && !pro.IsIced)
                    rets.Add(pro);
            }
        }

        if (range >= 2 && frameOf.IndexX - 1 >= 0)
        {
            int idxXLeft = frameOf.IndexX - 1;
            for (int y = 0; y < CountY; ++y)
            {
                Product pro = mFrames[idxXLeft, y].ChildProduct;
                if (pro != null && !pro.IsLocked() && !pro.IsChocoBlock() && !pro.IsIced)
                    rets.Add(pro);
            }
        }

        return rets.ToArray();
    }
    private Product[] ScanAroundProducts(Product target, int round)
    {
        if (target == null)
            return null;

        List<Product> rets = new List<Product>();
        Frame frameOf = target.ParentFrame != null ? target.ParentFrame : GetFrame(target.transform.position.x, target.transform.position.y);
        int idxX = frameOf.IndexX;
        int idxY = frameOf.IndexY;
        for (int y = idxY - round; y < idxY + round + 1; ++y)
        {
            for (int x = idxX - round; x < idxX + round + 1; ++x)
            {
                Frame frame = GetFrame(x, y);
                if (frame == null)
                    continue;

                Product pro = frame.ChildProduct;
                if (pro != null && !pro.IsLocked() && !pro.IsChocoBlock() && !pro.IsIced)
                    rets.Add(pro);
            }
        }
        return rets.ToArray();
    }

    public Product[] BreakSkilledProduct(Product skilledProduct)
    {
        DestroyProducts(new Product[1] { skilledProduct });

        if (skilledProduct.mSkill == ProductSkill.Horizontal)
        {
            CreateStripeEffect(skilledProduct.transform.position, false);
            Product[] destroyes = ScanHorizenProducts(skilledProduct);
            DestroyProducts(destroyes);
            return destroyes;
        }
        else if (skilledProduct.mSkill == ProductSkill.Vertical)
        {
            CreateStripeEffect(skilledProduct.transform.position, true);
            Product[] destroyes = ScanVerticalProducts(skilledProduct);
            DestroyProducts(destroyes);
            return destroyes;
        }
        else if (skilledProduct.mSkill == ProductSkill.Bomb)
        {
            CreateExplosionEffect(skilledProduct.transform.position);
            Product[] destroyes = ScanAroundProducts(skilledProduct, 1);
            DestroyProducts(destroyes);
            return destroyes;
        }
        else if (skilledProduct.mSkill == ProductSkill.SameColor)
        {
            Vector3 startPos = skilledProduct.transform.position;
            List<Product> pros = new List<Product>();
            foreach (Frame frame in mFrames)
            {
                Product pro = frame.ChildProduct;
                if (pro == null || pro.IsLocked())
                    continue;

                pros.Add(pro);
            }

            List<Product[]> matches = FindMatchedProducts(pros.ToArray());
            pros.Clear();

            foreach (Product[] match in matches)
            {
                Vector3 destPos = match[0].transform.position;
                CreateLaserEffect(startPos, destPos);
                DestroyProducts(match);
                pros.AddRange(match);
            }

            return pros.ToArray();
        }
        return null;
    }
    IEnumerator LoopBreakSkill(Product[] skillProducts, Action EventLoopEnd)
    {
        mIsCycling = true;
        List<Product> skilledProducts = new List<Product>();
        skilledProducts.AddRange(skillProducts);

        while (true)
        {
            List<Product> destroies = new List<Product>();
            foreach (Product pro in skilledProducts)
                destroies.AddRange(BreakSkilledProduct(pro));

            skilledProducts.Clear();
            foreach (Product pro in destroies)
            {
                if (pro.mSkill != ProductSkill.Nothing)
                    skilledProducts.Add(pro);
            }

            if (skilledProducts.Count <= 0)
                break;

            yield return new WaitForSeconds(0.4f);
        }

        yield return new WaitForSeconds(0.2f);

        EventLoopEnd?.Invoke();
        mIsCycling = false;
    }
    public Product[] BreakSkilledProductBoth(Product productA, Product productB)
    {
        DestroyProducts(new Product[2] { productA, productB });

        if (productA.mSkill == ProductSkill.Bomb)
        {
            List<Product> pros = new List<Product>();
            if (productB.mSkill == ProductSkill.Horizontal)
            {
                Product[] destroyes = ScanHorizenProducts(productA, 2);
                DestroyProducts(destroyes);
                return destroyes;
            }
            else if (productB.mSkill == ProductSkill.Vertical)
            {
                Product[] destroyes = ScanVerticalProducts(productA, 2);
                DestroyProducts(destroyes);
                return destroyes;
            }
            else if (productB.mSkill == ProductSkill.Bomb)
            {
                Product[] destroyes = ScanAroundProducts(productA, 2);
                DestroyProducts(destroyes);
                return destroyes;
            }
        }
        else if (productA.mSkill == ProductSkill.Horizontal || productA.mSkill == ProductSkill.Vertical)
        {
            List<Product> destroyes = new List<Product>();
            Product[] rets = null;
            rets = ScanHorizenProducts(productA);
            destroyes.AddRange(DestroyProducts(rets));
            rets = ScanVerticalProducts(productA);
            destroyes.AddRange(DestroyProducts(rets));
            rets = ScanHorizenProducts(productB);
            destroyes.AddRange(DestroyProducts(rets));
            rets = ScanVerticalProducts(productB);
            destroyes.AddRange(DestroyProducts(rets));
            return destroyes.ToArray();
        }
        return null;
    }
    IEnumerator LoopBreakAllSkill()
    {
        while (true)
        {
            for(int y = CountY - 1; y >= 0; --y)
            {
                for (int x = 0; x < CountX; ++x)
                {
                    Frame frame = mFrames[x, y];
                    Product pro = frame.ChildProduct;
                    if (pro != null && pro.mSkill != ProductSkill.Nothing && !pro.IsLocked())
                    {
                        BreakSkilledProduct(pro);
                        //StartCoroutine(LoopBreakSkill(new Product[1] { pro }, null));
                        goto KeepLoop;
                    }
                }
            }

            break;

        KeepLoop:
            yield return new WaitForSeconds(0.4f);
        }
    }
    IEnumerator LoopSameColorSkill(Vector3 startPos)
    {
        while (true)
        {
            if (!IsDropFinish())
            {
                yield return null;
                continue;
            }

            bool end = true;
            foreach (Frame frame in mFrames)
            {
                if (frame.Empty)
                    continue;

                Product pro = frame.ChildProduct;
                if (pro != null && !pro.IsLocked())
                {
                    List<Product[]> matches = FindMatchedProducts(new Product[1] { pro });
                    if (matches.Count > 0)
                    {
                        Vector3 destPos = matches[0][0].transform.position;
                        CreateLaserEffect(startPos, destPos);
                        DestroyProducts(matches[0]);
                        end = false;
                    }
                }
            }

            if (end)
                break;
            else
                yield return new WaitForSeconds(0.3f);
        }
    }

    private Product[] ScanRandomProducts(int step)
    {
        List<Product> rets = new List<Product>();
        int totalCount = CountX * CountY;
        int curIdx = -1;
        while(true)
        {
            curIdx++;
            curIdx = UnityEngine.Random.Range(curIdx, curIdx + step);
            if (curIdx >= totalCount)
                break;

            int idxX = curIdx % CountX;
            int idxY = curIdx / CountX;
            Product pro = mFrames[idxX, idxY].ChildProduct;
            if (pro != null && !pro.IsLocked() && !pro.IsChocoBlock() && !pro.IsIced)
                rets.Add(pro);
        }
        return rets.ToArray();
    }
    public void BreakSameColorBoth(Product productA, Product productB)
    {
        DestroyProducts(new Product[2] { productA, productB });

        if (productA.mSkill == ProductSkill.SameColor)
        {
            if (productB.mSkill == ProductSkill.Horizontal || productB.mSkill == ProductSkill.Vertical)
            {
                Product[] randomProducts = ScanRandomProducts(5);
                foreach(Product pro in randomProducts)
                {
                    CreateLaserEffect(productA.transform.position, pro.transform.position);
                    pro.ChangeProductImage(UnityEngine.Random.Range(0, 2) == 0 ? ProductSkill.Horizontal : ProductSkill.Vertical);
                }
                StartCoroutine(LoopBreakAllSkill());
            }
            else if (productB.mSkill == ProductSkill.Bomb)
            {
                Product[] randomProducts = ScanRandomProducts(5);
                foreach (Product pro in randomProducts)
                {
                    CreateLaserEffect(productA.transform.position, pro.transform.position);
                    pro.ChangeProductImage(ProductSkill.Bomb);
                }
                StartCoroutine(LoopBreakAllSkill());
            }
            else if (productB.mSkill == ProductSkill.SameColor)
            {
                StartCoroutine(LoopSameColorSkill(productA.transform.position));
            }
        }
    }
    private void SwipeSkilledProducts(Product main, Product sub)
    {
        if (main.mSkill == ProductSkill.SameColor)
        {
            BreakSameColorBoth(main, sub);
        }
        else if (sub.mSkill == ProductSkill.SameColor)
        {
            BreakSameColorBoth(sub, main);
        }
        else
        {
            Product[] destroies = null;
            switch (sub.mSkill)
            {
                case ProductSkill.Horizontal:   destroies = BreakSkilledProductBoth(main, sub); break;
                case ProductSkill.Vertical:     destroies = BreakSkilledProductBoth(main, sub); break;
                case ProductSkill.Bomb:         destroies = BreakSkilledProductBoth(sub, main); break;
            }

            List<Product> skillProducts = new List<Product>();
            foreach (Product pro in destroies)
            {
                if (pro.mSkill != ProductSkill.Nothing)
                    skillProducts.Add(pro);
            }

            StartCoroutine(UnityUtils.CallAfterSeconds(0.3f, () =>
            {
                StartCoroutine(LoopBreakSkill(skillProducts.ToArray(), null));
            }));
        }
    }


    private Queue<Product> FindAliveProducts(Frame[] subFrames)
    {
        Queue<Product> aliveProducts = new Queue<Product>();
        foreach (Frame frame in subFrames)
        {
            Product pro = frame.ChildProduct;
            if (pro != null)
                aliveProducts.Enqueue(pro);
        }
        return aliveProducts;
    }
    private List<Product[]> FindMatchedProducts(Product[] targetProducts)
    {
        Dictionary<Product, int> matchedPro = new Dictionary<Product, int>();
        List<Product[]> list = new List<Product[]>();
        foreach (Product pro in targetProducts)
        {
            if (matchedPro.ContainsKey(pro))
                continue;

            List<Product> matches = new List<Product>();
            pro.SearchMatchedProducts(matches, pro.mColor);
            if (matches.Count >= UserSetting.MatchCount)
            {
                list.Add(matches.ToArray());
                foreach (Product sub in matches)
                    matchedPro[sub] = 1;
            }
        }
        return list;
    }
    private List<Product> FindAroundProducts(Frame[] emptyFrames)
    {
        Dictionary<Product, int> aroundProducts = new Dictionary<Product, int>();
        foreach (Frame frame in emptyFrames)
        {
            Frame[] aroundFrames = frame.GetAroundFrames();
            foreach (Frame sub in aroundFrames)
            {
                Product pro = sub.ChildProduct;
                if (pro != null && !pro.IsLocked())
                    aroundProducts[pro] = 1;
            }
        }
        return new List<Product>(aroundProducts.Keys);
    }
    public Frame GetFrame(float worldPosX, float worldPosY)
    {
        Rect worldRect = FieldWorldRect;
        if (worldPosX < worldRect.xMin || worldPosY < worldRect.yMin || worldRect.xMax < worldPosX || worldRect.yMax < worldPosY)
            return null;

        float idxX = (worldPosX - worldRect.xMin) / GridSize;
        float idxY = (worldPosY - worldRect.yMin) / GridSize;
        return mFrames[(int)idxX, (int)idxY];
    }
    private void Attack(int score, Vector3 fromPos)
    {
        int point = score / UserSetting.AttackScore;
        if (point <= 0)
            return;

        int remainPt = AttackPoints.Count;
        if (remainPt <= 0)
        {
            Opponent.Damaged(point, fromPos);
        }
        else
        {
            AttackPoints.Add(-point, fromPos);
        }
    }
    private void Damaged(int point, Vector3 fromPos)
    {
        AttackPoints.Add(point, fromPos);
        if(FieldType == GameFieldType.pvpPlayer)
        {
            StopCoroutine("FlushAttacks");
            StartCoroutine("FlushAttacks");
        }
    }
    private IEnumerator FlushAttacks()
    {
        while (AttackPoints.Count > 0)
        {
            if (AttackPoints.IsReady && IsIdle)
            {
                int cnt = AttackPoints.Pop(UserSetting.FlushCount);
                List<Product> products = GetNextFlushTargets(cnt);
                Network_FlushAttacks(Serialize(products.ToArray()));
                foreach (Product pro in products)
                    pro.SetChocoBlock(1, true);

                yield return new WaitForSeconds(2.0f);
            }
            else
                yield return null;

        }
    }
    private IEnumerator CheckFinishGame()
    {
        EventRemainTime?.Invoke(mStageInfo.TimeLimit);

        int preRemainSec = 0;
        while(true)
        {
            yield return null;

            if (mStageInfo.TimeLimit > 0)
            {
                int currentPlaySec = (int)new TimeSpan((DateTime.Now - mStartTime).Ticks).TotalSeconds;
                int remainSec = mStageInfo.TimeLimit - currentPlaySec;
                if (remainSec != preRemainSec && remainSec >= 0)
                {
                    preRemainSec = remainSec;
                    EventRemainTime?.Invoke(remainSec);
                }

                if(remainSec < 0 && IsIdle)
                {
                    bool isWin = Billboard.CurrentScore > Opponent.Billboard.CurrentScore;
                    EventFinish?.Invoke(isWin);
                    FinishGame();
                    break;
                }
            }
            else if(mStageInfo.MoveLimit > 0)
            {
                if (Billboard.MoveCount >= mStageInfo.MoveLimit && IsIdle)
                {
                    EventFinish?.Invoke(false);
                    FinishGame();
                    break;
                }
            }

            if (FieldType == GameFieldType.Stage)
                CheckStageFinish();
            else if (FieldType == GameFieldType.pvpPlayer)
                CheckPVPFinish();
        }
    }
    private void CheckStageFinish()
    {
        bool isSuccess = false;
        int targetCount = mStageInfo.GoalValue;
        int comboTypeCount = mStageInfo.ComboTypeCount();

        switch (mStageInfo.GoalTypeEnum)
        {
            case StageGoalType.Score:
                if (Billboard.CurrentScore >= targetCount * UserSetting.ScorePerBar)
                    isSuccess = true;
                break;
            case StageGoalType.Combo:
                if (Billboard.ComboCounter[comboTypeCount] >= targetCount)
                    isSuccess = true;
                break;
            case StageGoalType.ItemOneMore:
                if (Billboard.ItemOneMoreCount >= targetCount)
                    isSuccess = true;
                break;
            case StageGoalType.ItemKeepCombo:
                if (Billboard.ItemKeepComboCount >= targetCount)
                    isSuccess = true;
                break;
            case StageGoalType.ItemSameColor:
                if (Billboard.ItemSameColorCount >= targetCount)
                    isSuccess = true;
                break;
            case StageGoalType.Cover:
                if (Billboard.CoverCount >= targetCount)
                    isSuccess = true;
                break;
            case StageGoalType.Choco:
                if (Billboard.ChocoCount >= targetCount)
                    isSuccess = true;
                break;
        }

        if (isSuccess)
        {
            EventFinish?.Invoke(true);
            FinishGame();
        }

        return;
    }
    private void CheckPVPFinish()
    {
        if (Opponent.AttackPoints.Count > 200)
        {
            EventFinish?.Invoke(true);
            FinishGame();
            return;
        }

        int counter = 0;
        foreach (Frame frame in mFrames)
        {
            if (frame.Empty)
                continue;

            counter++;
            Product pro = frame.ChildProduct;
            if (pro != null && pro.IsChocoBlock())
                counter--;
        }

        if(counter == 0)
        {
            EventFinish?.Invoke(false);
            FinishGame();
        }
        
        return;
    }
    private List<Product> GetNextFlushTargets(int cnt)
    {
        List<Product> products = new List<Product>();
        for(int y = 0; y < CountY; ++y)
        {
            for (int x = 0; x < CountX; ++x)
            {
                Frame frame = mFrames[x, y];
                if (frame.Empty)
                    continue;
                Product pro = frame.ChildProduct;
                if (pro == null || pro.IsChocoBlock())
                    continue;

                products.Add(pro);
                if (products.Count >= cnt)
                    return products;
            }
        }
        return products;
    }
    private void ReduceTargetScoreCombo(Product pro, int preScore, int nextScore)
    {
        if (mStageInfo.GoalTypeEnum == StageGoalType.Score)
        {
            int newStarCount = nextScore / UserSetting.ScorePerBar - preScore / UserSetting.ScorePerBar;
            for (int i = 0; i < newStarCount; ++i)
                EventBreakTarget?.Invoke(pro.transform.position, StageGoalType.Score);

        }
        else if (mStageInfo.GoalTypeEnum == StageGoalType.Combo)
        {
            string goalType = mStageInfo.GoalType;
            int targetCombo = int.Parse(goalType[goalType.Length - 1].ToString());
            int curCombo = Billboard.CurrentCombo;
            if (targetCombo == curCombo)
                EventBreakTarget?.Invoke(pro.transform.position, StageGoalType.Combo);
        }
    }
    private ProductSkill CheckSkillable(Product[] matches)
    {
        if (matches.Length <= UserSetting.MatchCount)
            return ProductSkill.Nothing;

        if (matches.Length >= 5)
            return ProductSkill.SameColor;

        ProductSkill skill = ProductSkill.Nothing;
        int ran = UnityEngine.Random.Range(0, 3);
        if (ran == 0)
            skill = ProductSkill.Horizontal;
        else if (ran == 1)
            skill = ProductSkill.Vertical;
        else
            skill = ProductSkill.Bomb;

        return skill;
    }
    private List<Product> GetSameColorProducts(ProductColor color)
    {
        List<Product> list = new List<Product>();
        foreach (Frame frame in mFrames)
        {
            if (frame.Empty || frame.ChildProduct == null || frame.ChildProduct.IsLocked() || frame.ChildProduct.IsChocoBlock())
                continue;
            if (frame.ChildProduct.mColor != color)
                continue;
            list.Add(frame.ChildProduct);
        }
        return list;
    }
    private void MakeSkillProduct(int matchedCount)
    {
        if (matchedCount <= UserSetting.MatchCount)
            return;

        ProductSkill skill = ProductSkill.Nothing;
        if (mStageInfo.Items.ContainsKey(matchedCount))
            skill = mStageInfo.Items[matchedCount];
        else if (mStageInfo.Items.ContainsKey(-1))
            skill = mStageInfo.Items[-1];

        if (skill == ProductSkill.Nothing)
            return;

        mNextSkills.Enqueue(skill);
    }
    private bool IsDropFinish()
    {
        for (int x = 0; x < CountX; ++x)
        {
            Frame frame = mFrames[x, CountY - 1];
            if (frame.Empty)
                continue;
            if (frame.ChildProduct == null)
                return false;
        }
        return true;
    }
    private bool IsAllIdle()
    {
        foreach(Frame frame in mFrames)
        {
            if (frame.Empty)
                continue;
            if (frame.ChildProduct == null || frame.ChildProduct.IsLocked())
                return false;
        }
        return true;
    }
    private void SecondaryInitFrames()
    {
        for(int x = 0; x < CountX; ++x)
        {
            SpriteMask mask = null;
            int maskOrder = 0;
            for (int y = 0; y < CountY; ++y)
            {
                Frame curFrame = mFrames[x, y];
                if (curFrame.Empty)
                    continue;

                Frame subTopFrame = SubTopFrame(curFrame);
                if (curFrame.Down() == null)
                {
                    int height = subTopFrame.IndexY - curFrame.IndexY + 1;
                    Vector3 centerPos = (curFrame.transform.position + subTopFrame.transform.position) * 0.5f;
                    mask = CreateMask(centerPos, height, maskOrder);
                    maskOrder++;
                }
                curFrame.SetSubTopFrame(subTopFrame);
                curFrame.SetSpriteMask(mask);

                if (curFrame.Left() == null) curFrame.ShowBorder(0);
                if (curFrame.Right() == null) curFrame.ShowBorder(1);
                if (curFrame.Up() == null) curFrame.ShowBorder(2);
                if (curFrame.Down() == null) curFrame.ShowBorder(3);
            }
        }
    }
    private void InitDropGroupFrames()
    {
        List<VerticalFrames> groups = new List<VerticalFrames>();
        VerticalFrames group = new VerticalFrames();
        for (int x = 0; x < CountX; ++x)
        {
            List<Frame> frames = new List<Frame>();
            for (int y = 0; y < CountY; ++y)
            {
                Frame curFrame = mFrames[x, y];
                if (curFrame.Empty)
                    continue;

                Frame up = curFrame.Up();
                if (up == null || up.Empty)
                {
                    curFrame.VertFrames = group;
                    frames.Add(curFrame);

                    group.Frames = frames.ToArray();
                    groups.Add(group);
                    frames.Clear();
                    group = new VerticalFrames();
                }
                else
                {
                    curFrame.VertFrames = group;
                    frames.Add(curFrame);
                }
            }
        }

        mFrameDropGroup = groups.ToArray();
    }
    private Frame SubTopFrame(Frame baseFrame)
    {
        Frame curFrame = baseFrame;
        while (true)
        {
            if (curFrame.Up() == null)
                break;
            else
                curFrame = curFrame.Up();
        }
        return curFrame;
    }
    private SpriteMask CreateMask(Vector3 pos, float height, int layerOrder)
    {
        GameObject maskObj = Instantiate(MaskPrefab, transform);
        maskObj.transform.position = pos;
        maskObj.transform.localScale = new Vector3(1, height, 1);

        SpriteMask mask = maskObj.GetComponent<SpriteMask>();
        mask.isCustomRangeActive = true;
        mask.frontSortingOrder = layerOrder + 1;
        mask.backSortingOrder = layerOrder;
        return mask;
    }
    private Product CreateNewProduct(Frame parent, ProductColor color = ProductColor.None)
    {
        int typeIdx = color == ProductColor.None ? RandomNextColor() : (int)color - 1;
        GameObject obj = GameObject.Instantiate(ProductPrefabs[typeIdx], parent.transform, false);
        Product product = obj.GetComponent<Product>();
        product.transform.localPosition = new Vector3(0, 0, -1);
        product.AttachTo(parent);
        return product;
    }
    private Product CreateNewProduct(ProductColor color = ProductColor.None)
    {
        int typeIdx = color == ProductColor.None ? RandomNextColor() : (int)color - 1;
        GameObject obj = Instantiate(ProductPrefabs[typeIdx], transform);
        Product product = obj.GetComponent<Product>();
        return product;
    }
    private int RandomNextColor()
    {
        int count = (int)(mStageInfo.ColorCount + 0.99f);
        float remain = mStageInfo.ColorCount - (int)mStageInfo.ColorCount;
        int idx = UnityEngine.Random.Range(0, count);
        if (remain > 0 && idx == count - 1)
        {
            if (remain <= UnityEngine.Random.Range(0, 10) * 0.1f)
                idx = UnityEngine.Random.Range(0, count - 1);
        }
        return idx;
    }
    private void ResetGame()
    {
        int cnt = transform.childCount;
        for (int i = 0; i < cnt; ++i)
            Destroy(transform.GetChild(i).gameObject);

        foreach (GameObject skill in SkillSlots)
            skill.SetActive(false);

        EventBreakTarget = null;
        EventMatched = null;
        EventFinish = null;
        EventReduceLimit = null;

        AttackPoints = null;
        mMoveLock = false;
        mIsCycling = false;
        mIsSwipping = false;
        mRemoveBadEffectsCoolTime = false;

        Billboard.Reset();
        mNetMessages.Clear();
        mNextSkills.Clear();

        mFrameDropGroup = null;
        mFrames = null;
        mUserInfo = null;
        mStageInfo = null;

        StopAllCoroutines();
    }
    private void RemoveLimit()
    {
        if(mStageInfo.MoveLimit > 0)
        {
            Billboard.MoveCount++;
            mMoveLock = Billboard.MoveCount >= mStageInfo.MoveLimit;
            EventReduceLimit?.Invoke();
        }
    }
    public int NextMatchCount(Product pro, SwipeDirection dir)
    {
        Product target = pro.Dir(dir);
        if (target == null || target.mColor == pro.mColor)
            return 0;

        List<Product> matches = new List<Product>();
        Product[] pros = target.GetAroundProducts(target.ParentFrame);
        foreach(Product each in pros)
        {
            if (each == pro)
                continue;

            each.SearchMatchedProducts(matches, pro.mColor);
        }
        return matches.Count;
    }
    private Frame[] GetRandomIdleFrames(int count)
    {
        Dictionary<int, Frame> rets = new Dictionary<int, Frame>();
        int totalCount = CountX * CountY;
        int loopCount = 0;
        while(rets.Count < count && loopCount < totalCount)
        {
            loopCount++;
            int ranIdx = UnityEngine.Random.Range(0, totalCount);
            if (rets.ContainsKey(ranIdx))
                continue;

            int idxX = ranIdx % CountX;
            int idxY = ranIdx / CountX;
            Product pro = mFrames[idxX, idxY].ChildProduct;
            if (pro == null || pro.IsLocked())
                continue;

            rets[ranIdx] = pro.ParentFrame;
        }

        return new List<Frame>(rets.Values).ToArray();
    }
    void CastSkill(List<Product[]> nextProducts)
    {
        if (Billboard.CurrentCombo > 3)
            return;

        foreach(Product[] pros in nextProducts)
        {
            SkillPair skillPair = SkillMapping[(int)pros[0].mColor];
            PVPCommand skill = skillPair.Item1;
            if (skill == PVPCommand.Undef)
                continue;

            switch(skill)
            {
                case PVPCommand.SkillBomb: CastSkillBomb(pros); break;
                case PVPCommand.SkillIce: CastSkillice(pros); break;
                case PVPCommand.SkillShield: CastSkillShield(pros); break;
                case PVPCommand.SkillScoreBuff: CastSkillScoreBuff(pros); break;
                case PVPCommand.SkillCloud: CastSkillCloud(pros); break;
                case PVPCommand.SkillUpsideDown: CastSkillUpsideDown(pros); break;
                case PVPCommand.SkillRemoveBadEffects: CastSkillRemoveBadEffects(pros); break;
                default: break;
            }
        }
    }
    void CastSkillBomb(Product[] matches)
    {
        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectBadEffect);

        Vector3 startPos = matches[0].transform.position;
        Frame[] targetFrames = Opponent.GetRandomIdleFrames(Billboard.CurrentCombo * 2);
        foreach (Frame frame in targetFrames)
            CreateLaserEffect(startPos, frame.transform.position);

        if (!Opponent.DefenseShield(targetFrames))
        {
            foreach (Frame frame in targetFrames)
                CreateParticle(BombParticle, frame.transform.position);
        }

        Network_Skill(PVPCommand.SkillBomb, Serialize(targetFrames), matches[0].ParentFrame);
    }
    void CastSkillice(Product[] matches)
    {
        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectBadEffect);

        Vector3 pos = matches[0].transform.position;
        Frame[] targetFrames = Opponent.GetRandomIdleFrames(Billboard.CurrentCombo * 2);
        foreach (Frame frame in targetFrames)
            CreateLaserEffect(pos, frame.transform.position);

        Opponent.DefenseShield(targetFrames);

        Network_Skill(PVPCommand.SkillIce, Serialize(targetFrames), matches[0].ParentFrame);
    }
    void CastSkillShield(Product[] matches)
    {
        if (ShieldSlot.activeSelf)
            return;

        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectBadEffect);

        Vector3 pos = matches[0].transform.position;
        CreateLaserEffect(pos, ShieldSlot.transform.position);
        CreateParticle(ShieldParticle, pos);
        ShieldSlot.SetActive(true);

        Network_Skill(PVPCommand.SkillShield, Serialize(new List<Product>().ToArray()), matches[0].ParentFrame);
    }
    bool DefenseShield(Frame[] frames)
    {
        if (!ShieldSlot.activeSelf)
            return false;

        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectGoodEffect);

        ShieldSlot.SetActive(false);
        foreach (Frame frame in frames)
            CreateParticle(ShieldParticle, frame.transform.position);

        return true;
    }
    bool DefenseShield(Product[] pros)
    {
        if (!ShieldSlot.activeSelf)
            return false;

        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectGoodEffect);

        ShieldSlot.SetActive(false);
        foreach (Product pro in pros)
            CreateParticle(ShieldParticle, pro.transform.position);

        return true;
    }
    void CastSkillScoreBuff(Product[] matches)
    {
        if (ScoreBuffSlot.activeSelf)
            return;

        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectGoodEffect);

        Vector3 pos = matches[0].transform.position;
        CreateLaserEffect(pos, ScoreBuffSlot.transform.position);
        CreateParticle(ScoreBuffParticle, pos);
        ScoreBuffSlot.SetActive(true);
        StartCoroutine(UnityUtils.CallAfterSeconds(Billboard.CurrentCombo * 4, () =>
        {
            ScoreBuffSlot.SetActive(false);
        }));

        Network_Skill(PVPCommand.SkillScoreBuff, Serialize(new List<Product>().ToArray()), matches[0].ParentFrame);
    }
    void CastSkillChangeProducts(Product[] matches)
    {
        Vector3 pos = matches[0].transform.position;
        Frame[] targetFrames = Opponent.GetRandomIdleFrames(3);
        foreach (Frame frame in targetFrames)
            CreateLaserEffect(pos, frame.transform.position);

        Network_Skill(PVPCommand.SkillChangeProducts, Serialize(targetFrames), matches[0].ParentFrame);
    }
    void CastSkillCloud(Product[] matches)
    {
        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectBadEffect);

        Vector3 pos = matches[0].transform.position;
        Frame[] targetFrames = Opponent.GetRandomIdleFrames(3);
        for(int i = 0; i < targetFrames.Length; ++i)
        {
            targetFrames[i] = Opponent.mFrames[0, targetFrames[i].IndexY];
            CreateLaserEffect(pos, targetFrames[i].transform.position);
        }

        if(!Opponent.DefenseShield(targetFrames))
            Opponent.CreateCloud(targetFrames, Billboard.CurrentCombo);

        Network_Skill(PVPCommand.SkillCloud, Serialize(targetFrames), matches[0].ParentFrame);
    }
    void CreateCloud(Frame[] frames, float size)
    {
        foreach(Frame frame in frames)
        {
            Vector3 pos = frame.transform.position;
            pos.z -= 2;
            GameObject cloudObj = Instantiate(CloudPrefab, pos, Quaternion.identity, transform);
            cloudObj.transform.localScale = new Vector3(size, size, 1);
            cloudObj.GetComponent<EffectCloud>().LimitWorldPosX = mFrames[CountX - 1, 0].transform.position.x;
        }
    }
    void CastSkillUpsideDown(Product[] matches)
    {
        if (Opponent.UpsideDownSlot.activeSelf)
        {
            SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectCooltime);
            return;
        }

        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectBadEffect);

        Vector3 pos = matches[0].transform.position;
        Frame destFrame = Opponent.CenterFrame;
        CreateLaserEffect(pos, destFrame.transform.position);
        
        if (!Opponent.DefenseShield(new Frame[1] { destFrame }))
        {
            CreateParticle(UpsideDownParticle, destFrame.transform.position);
            Opponent.UpsideDownSlot.SetActive(true);
            Opponent.StartCoroutine(UnityUtils.CallAfterSeconds(Billboard.CurrentCombo * 3, () =>
            {
                Opponent.UpsideDownSlot.SetActive(false);
            }));
        }

        Network_Skill(PVPCommand.SkillUpsideDown, Serialize(new List<Product>().ToArray()), matches[0].ParentFrame);
    }
    void CastSkillRemoveBadEffects(Product[] matches)
    {
        if (mRemoveBadEffectsCoolTime)
        {
            SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectCooltime);
            return;
        }

        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectGoodEffect);

        Vector3 pos = matches[0].transform.position;
        CreateParticle(RemoveBadEffectParticle, pos);
        RemoveBadEffects(pos);

        mRemoveBadEffectsCoolTime = true;
        StartCoroutine(UnityUtils.CallAfterSeconds(8.0f, () =>
        {
            mRemoveBadEffectsCoolTime = false;
        }));

        Network_Skill(PVPCommand.SkillRemoveBadEffects, Serialize(new List<Product>().ToArray()), matches[0].ParentFrame);
    }
    void RemoveBadEffects(Vector3 startPos)
    {
        foreach (Frame frame in mFrames)
        {
            Product pro = frame.ChildProduct;
            if (pro != null && pro.IsChocoBlock())
            {
                CreateLaserEffect(startPos, pro.ParentFrame.transform.position);
                pro.BreakChocoBlock(100);
            }
        }

        EffectCloud[] clouds = GetComponentsInChildren<EffectCloud>();
        foreach (EffectCloud cloud in clouds)
        {
            CreateLaserEffect(startPos, cloud.transform.position);
            Destroy(cloud.gameObject);
        }

        if(UpsideDownSlot.activeSelf)
        {
            CreateLaserEffect(startPos, UpsideDownSlot.transform.position);
            UpsideDownSlot.SetActive(false);
        }
    }
    void CreateLaserEffect(Vector2 startPos, Vector2 destPos)
    {
        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectBadEffect);
        Vector3 start = new Vector3(startPos.x, startPos.y, -4.0f);
        Vector3 dest = new Vector3(destPos.x, destPos.y, -4.0f);
        GameObject laserObj = GameObject.Instantiate(LaserParticle, start, Quaternion.identity, transform);
        laserObj.GetComponent<EffectLaser>().SetDestination(dest);
    }
    void CreateSparkEffect(Vector2 startPos)
    {
        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectBadEffect);
        Vector3 start = new Vector3(startPos.x, startPos.y, -4.0f);
        GameObject obj = GameObject.Instantiate(SparkParticle, start, Quaternion.identity, transform);
        Destroy(obj, 1.0f);
    }
    void CreateStripeEffect(Vector2 startPos, bool isVertical)
    {
        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectBadEffect);
        Vector3 start = new Vector3(startPos.x, startPos.y, -4.0f);
        GameObject obj = GameObject.Instantiate(StripeParticle, start, isVertical ? Quaternion.Euler(0, 0, 90) : Quaternion.identity, transform);
        Destroy(obj, 1.0f);
    }
    void CreateMergeEffect(Product productA, Product productB)
    {
        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectBadEffect);
        Vector3 pos = (productA.transform.position + productB.transform.position) * 0.5f;
        pos.z = -4.0f;
        GameObject obj = GameObject.Instantiate(MergeParticle, pos, Quaternion.identity, transform);
        obj.GetComponent<EffectMerge>().SetProucts(productA, productB);
    }
    void CreateExplosionEffect(Vector2 startPos)
    {
        SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectBadEffect);
        Vector3 start = new Vector3(startPos.x, startPos.y, -4.0f);
        GameObject obj = GameObject.Instantiate(ExplosionParticle, start, Quaternion.identity, transform);
        Destroy(obj, 1.0f);
    }
    void CreateParticle(GameObject prefab, Vector3 worldPos)
    {
        worldPos.z -= 1;
        Instantiate(prefab, worldPos, Quaternion.identity, transform);
    }
    #endregion

    #region Network
    public void HandlerNetworkMessage(Header head, byte[] body)
    {
        if (!gameObject.activeInHierarchy)
            return;
        if (head.Ack == 1)
            return;
        if (head.Cmd != NetCMD.PVP)
            return;

        PVPInfo resMsg = Utils.Deserialize<PVPInfo>(ref body);
        if (resMsg.cmd == PVPCommand.EndGame)
        {
            mNetMessages.AddFirst(resMsg);
        }
        else
        {
            mNetMessages.AddLast(resMsg);
        }
    }
    IEnumerator ProcessNetMessages()
    {
        while (true)
        {
            yield return null;

            if (mNetMessages.Count == 0)
                continue;

            PVPInfo body = mNetMessages.First.Value;
            if (body.cmd == PVPCommand.EndGame)
            {
                EventFinish?.Invoke(body.success);
                FinishGame();
            }
            else if (body.cmd == PVPCommand.StartGame)
            {
                for (int i = 0; i < body.ArrayCount; ++i)
                {
                    ProductInfo info = body.products[i];
                    Frame frame = GetFrame(info.idxX, info.idxY);
                    Product pro = CreateNewProduct(frame, info.color);
                    pro.GetComponent<BoxCollider2D>().enabled = false;
                    pro.SetChocoBlock(0);
                    pro.EventUnWrapChoco = () => {
                        Billboard.ChocoCount++;
                        EventBreakTarget?.Invoke(pro.transform.position, StageGoalType.Choco);
                    };
                }
                mNetMessages.RemoveFirst();
            }
            else if (body.cmd == PVPCommand.Click)
            {
                //if(IsIdle)
                //{
                //    Product pro = GetFrame(body.products[0].idxX, body.products[0].idxY).ChildProduct;
                //    OnClick(pro.gameObject);
                //    mNetMessages.RemoveFirst();
                //}
                mNetMessages.RemoveFirst();
            }
            else if (body.cmd == PVPCommand.Swipe)
            {
                Product pro = GetFrame(body.products[0].idxX, body.products[0].idxY).ChildProduct;
                OnSwipe(pro.gameObject, body.dir);
                mNetMessages.RemoveFirst();
            }
            else if (body.cmd == PVPCommand.Destroy)
            {
                //List<Product> products = new List<Product>();
                //for (int i = 0; i < body.ArrayCount; ++i)
                //{
                //    ProductInfo info = body.products[i];
                //    Product pro = GetFrame(info.idxX, info.idxY).ChildProduct;
                //    if (pro != null && !pro.IsLocked() && info.color == pro.mColor)
                //        products.Add(pro);
                //}
                //
                //if (products.Count != body.ArrayCount)
                //    LOG.warn("Not Sync Destroy Products");
                //else
                //{
                //    Billboard.CurrentCombo = body.combo;
                //    DestroyProducts(products.ToArray(), body.skill);
                //    mNetMessages.RemoveFirst();
                //}
            }
            else if (body.cmd == PVPCommand.Create)
            {
                //Dictionary<Frame, ProductColor> newProducts = new Dictionary<Frame, ProductColor>();
                //for (int i = 0; i < body.ArrayCount; ++i)
                //{
                //    ProductInfo info = body.products[i];
                //    Frame frame = GetFrame(info.idxX, info.idxY);
                //    newProducts[frame] = info.color;
                //}
                //
                //if(IsReadyToNextDrop(newProducts))
                //{
                //    StartToDropAndCreateRemote(newProducts, durationDrop);
                //    mNetMessages.RemoveFirst();
                //}
            }
            else if (body.cmd == PVPCommand.FlushAttacks)
            {
                if(IsAllIdle())
                {
                    AttackPoints.Pop(body.ArrayCount);
                    for (int i = 0; i < body.ArrayCount; ++i)
                    {
                        ProductInfo info = body.products[i];
                        Product pro = GetFrame(info.idxX, info.idxY).ChildProduct;
                        pro.SetChocoBlock(1, true);
                    }

                    mNetMessages.RemoveFirst();
                }
            }
            else if (body.cmd == PVPCommand.SkillBomb)
            {
                //SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectBadEffect);
                //Vector3 startPos = GetFrame(body.idxX, body.idxY).transform.position;
                //List<Product> rets = new List<Product>();
                //for (int i = 0; i < body.ArrayCount; ++i)
                //{
                //    ProductInfo info = body.products[i];
                //    Frame frame = Opponent.GetFrame(info.idxX, info.idxY);
                //    CreateLaserEffect(startPos, frame.transform.position);
                //    Product pro = frame.ChildProduct;
                //    if (pro != null && !pro.IsLocked())
                //        rets.Add(pro);
                //}
                //
                //if (!Opponent.DefenseShield(rets.ToArray()))
                //{
                //    foreach(Product pro in rets)
                //        CreateParticle(BombParticle, pro.transform.position);
                //
                //    Opponent.DestroyProducts(rets.ToArray(), ProductSkill.Nothing);
                //    if (!Opponent.mIsCycling)
                //        Opponent.StartCoroutine(Opponent.DoDropCycle());
                //}
                //
                //mNetMessages.RemoveFirst();
            }
            else if (body.cmd == PVPCommand.SkillIce)
            {
                SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectBadEffect);
                Vector3 startPos = GetFrame(body.idxX, body.idxY).transform.position;
                List<Product> rets = new List<Product>();
                for (int i = 0; i < body.ArrayCount; ++i)
                {
                    ProductInfo info = body.products[i];
                    Frame frame = Opponent.GetFrame(info.idxX, info.idxY);
                    CreateLaserEffect(startPos, frame.transform.position);
                    Product pro = frame.ChildProduct;
                    if (pro != null && !pro.IsLocked())
                        rets.Add(pro);
                }

                if (!Opponent.DefenseShield(rets.ToArray()))
                {
                    foreach (Product pro in rets)
                    {
                        pro.SetIce(true);
                        pro.StartCoroutine(UnityUtils.CallAfterSeconds(5.0f, () => {
                            pro.SetIce(false);
                        }));
                    }

                    Network_Skill(PVPCommand.SkillIceRes, Serialize(rets.ToArray()));
                }

                mNetMessages.RemoveFirst();
            }
            else if (body.cmd == PVPCommand.SkillIceRes)
            {
                for (int i = 0; i < body.ArrayCount; ++i)
                {
                    ProductInfo info = body.products[i];
                    Product pro = GetFrame(info.idxX, info.idxY).ChildProduct;
                    if (pro != null && !pro.IsLocked() && info.color == pro.mColor)
                    {
                        pro.SetIce(true);
                        pro.StartCoroutine(UnityUtils.CallAfterSeconds(5.0f, () => {
                            pro.SetIce(false);
                        }));
                    }
                }

                mNetMessages.RemoveFirst();
            }
            else if (body.cmd == PVPCommand.SkillShield)
            {
                Vector3 startPos = GetFrame(body.idxX, body.idxY).transform.position;
                CreateLaserEffect(startPos, ShieldSlot.transform.position);
                CreateParticle(ShieldParticle, startPos);
                ShieldSlot.SetActive(true);

                mNetMessages.RemoveFirst();
            }
            else if (body.cmd == PVPCommand.SkillScoreBuff)
            {
                Vector3 startPos = GetFrame(body.idxX, body.idxY).transform.position;
                CreateLaserEffect(startPos, ScoreBuffSlot.transform.position);
                CreateParticle(ScoreBuffParticle, startPos);
                ScoreBuffSlot.SetActive(true);
                StartCoroutine(UnityUtils.CallAfterSeconds(body.combo * 4, () =>
                {
                    ScoreBuffSlot.SetActive(false);
                }));

                mNetMessages.RemoveFirst();
            }
            else if (body.cmd == PVPCommand.SkillChangeProducts)
            {
                mNetMessages.RemoveFirst();
            }
            else if (body.cmd == PVPCommand.SkillCloud)
            {
                SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectBadEffect);
                Vector3 startPos = GetFrame(body.idxX, body.idxY).transform.position;
                List<Frame> frames = new List<Frame>();
                for (int i = 0; i < body.ArrayCount; ++i)
                {
                    ProductInfo info = body.products[i];
                    Frame frame = Opponent.GetFrame(info.idxX, info.idxY);
                    CreateLaserEffect(startPos, frame.transform.position);
                    frames.Add(frame);
                }

                if (!Opponent.DefenseShield(frames.ToArray()))
                {
                    Opponent.CreateCloud(frames.ToArray(), body.combo);
                }

                mNetMessages.RemoveFirst();
            }
            else if (body.cmd == PVPCommand.SkillUpsideDown)
            {
                SoundPlayer.Inst.PlaySoundEffect(SoundPlayer.Inst.EffectBadEffect);
                Vector3 startPos = GetFrame(body.idxX, body.idxY).transform.position;
                Frame destFrame = Opponent.CenterFrame;
                CreateLaserEffect(startPos, destFrame.transform.position);

                if (!Opponent.DefenseShield(new Frame[1] { destFrame }))
                {
                    CreateParticle(UpsideDownParticle, destFrame.transform.position);
                    Opponent.UpsideDownSlot.SetActive(true);
                    Opponent.StartCoroutine(UnityUtils.CallAfterSeconds(body.combo * 4, () =>
                    {
                        Opponent.UpsideDownSlot.SetActive(false);
                    }));
                }

                mNetMessages.RemoveFirst();
            }
            else if (body.cmd == PVPCommand.SkillRemoveBadEffects)
            {
                Vector3 startPos = GetFrame(body.idxX, body.idxY).transform.position;
                CreateParticle(RemoveBadEffectParticle, startPos);
                RemoveBadEffects(startPos);
                mNetMessages.RemoveFirst();
            }
        }
    }
    private ProductInfo[] Serialize(Product[] pros)
    {
        List<ProductInfo> infos = new List<ProductInfo>();
        for (int i = 0; i < pros.Length; ++i)
        {
            ProductInfo info = new ProductInfo();
            info.idxX = pros[i].ParentFrame.IndexX;
            info.idxY = pros[i].ParentFrame.IndexY;
            info.color = pros[i].mColor;
            infos.Add(info);
        }
        return infos.ToArray();
    }
    private ProductInfo[] Serialize(Frame[] frames)
    {
        List<ProductInfo> infos = new List<ProductInfo>();
        for (int i = 0; i < frames.Length; ++i)
        {
            ProductInfo info = new ProductInfo();
            info.idxX = frames[i].IndexX;
            info.idxY = frames[i].IndexY;
            info.color = ProductColor.None;
            infos.Add(info);
        }
        return infos.ToArray();
    }
    private void Network_StartGame(ProductInfo[] pros)
    {
        if (FieldType != GameFieldType.pvpPlayer)
            return;

        PVPInfo req = new PVPInfo();
        req.cmd = PVPCommand.StartGame;
        req.oppUserPk = InstPVP_Opponent.UserPk;
        req.XCount = CountX;
        req.YCount = CountY;
        req.colorCount = mStageInfo.ColorCount;
        req.combo = 0;
        req.ArrayCount = pros.Length;
        Array.Copy(pros, req.products, pros.Length);

        NetClientApp.GetInstance().Request(NetCMD.PVP, req, null);
    }
    private void Network_Click(Product pro)
    {
        if (FieldType != GameFieldType.pvpPlayer)
            return;

        PVPInfo req = new PVPInfo();
        req.cmd = PVPCommand.Click;
        req.oppUserPk = InstPVP_Opponent.UserPk;
        req.combo = pro.Combo;
        req.ArrayCount = 1;
        req.products[0].idxX = pro.ParentFrame.IndexX;
        req.products[0].idxY = pro.ParentFrame.IndexY;
        req.products[0].color = pro.mColor;
        NetClientApp.GetInstance().Request(NetCMD.PVP, req, null);
    }
    private void Network_Swipe(Product pro, SwipeDirection dir)
    {
        if (FieldType != GameFieldType.pvpPlayer)
            return;

        PVPInfo req = new PVPInfo();
        req.cmd = PVPCommand.Swipe;
        req.oppUserPk = InstPVP_Opponent.UserPk;
        req.combo = pro.Combo;
        req.ArrayCount = 1;
        req.dir = dir;
        req.products[0].idxX = pro.ParentFrame.IndexX;
        req.products[0].idxY = pro.ParentFrame.IndexY;
        req.products[0].color = pro.mColor;
        NetClientApp.GetInstance().Request(NetCMD.PVP, req, null);
    }
    private void Network_Destroy(ProductInfo[] pros, ProductSkill skill)
    {
        if (FieldType != GameFieldType.pvpPlayer)
            return;

        PVPInfo req = new PVPInfo();
        req.cmd = PVPCommand.Destroy;
        req.oppUserPk = InstPVP_Opponent.UserPk;
        req.combo = Billboard.CurrentCombo;
        req.skill = skill;
        req.ArrayCount = pros.Length;
        Array.Copy(pros, req.products, pros.Length);
        NetClientApp.GetInstance().Request(NetCMD.PVP, req, null);
    }
    private void Network_Create(ProductInfo[] pros)
    {
        if (FieldType != GameFieldType.pvpPlayer)
            return;

        PVPInfo req = new PVPInfo();
        req.cmd = PVPCommand.Create;
        req.oppUserPk = InstPVP_Opponent.UserPk;
        req.combo = 0;
        req.ArrayCount = pros.Length;
        Array.Copy(pros, req.products, pros.Length);
        NetClientApp.GetInstance().Request(NetCMD.PVP, req, null);
    }
    private void Network_FlushAttacks(ProductInfo[] pros)
    {
        if (FieldType != GameFieldType.pvpPlayer)
            return;

        PVPInfo req = new PVPInfo();
        req.cmd = PVPCommand.FlushAttacks;
        req.oppUserPk = InstPVP_Opponent.UserPk;
        req.combo = Billboard.CurrentCombo;
        req.ArrayCount = pros.Length;
        Array.Copy(pros, req.products, pros.Length);
        NetClientApp.GetInstance().Request(NetCMD.PVP, req, null);
    }
    private void Network_Skill(PVPCommand skill, ProductInfo[] infos, Frame startFrame = null)
    {
        PVPInfo req = new PVPInfo();
        req.cmd = skill;
        req.oppUserPk = InstPVP_Opponent.UserPk;
        req.ArrayCount = infos.Length;
        req.combo = Billboard.CurrentCombo;
        Array.Copy(infos, req.products, infos.Length);
        req.idxX = startFrame == null ? 0 : startFrame.IndexX;
        req.idxY = startFrame == null ? 0 : startFrame.IndexY;
        NetClientApp.GetInstance().Request(NetCMD.PVP, req, null);
    }
    #endregion
}
