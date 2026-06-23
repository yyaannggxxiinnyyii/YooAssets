using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在商店房中按当前商品数量自动生成并排布场景商品槽位。
/// </summary>
public sealed class StageShopOfferLayoutController : MonoBehaviour
{
    [Header("商品槽位")]
    [Tooltip("运行时实例化的商品槽位预制体，根节点需要挂 StageShopOfferPoint，并包含 StageShopOfferView。")]
    [SerializeField] private StageShopOfferPoint offerPointPrefab;
    [Tooltip("商品槽位实例的父节点；为空时使用当前对象。")]
    [SerializeField] private Transform offerRoot;

    [Header("自动排布")]
    [Tooltip("每行最多显示的商品数量，小于 1 时按 1 处理。")]
    [SerializeField] private int maxColumns = 4;
    [Tooltip("同一行商品之间的横向间距。")]
    [SerializeField] private float horizontalSpacing = 1.8f;
    [Tooltip("不同行商品之间的纵向间距。")]
    [SerializeField] private float verticalSpacing = 1.6f;
    [Tooltip("整体商品网格的本地起点偏移。")]
    [SerializeField] private Vector3 layoutOffset;
    [Tooltip("是否让每一行围绕 X 轴居中排布。")]
    [SerializeField] private bool centerRows = true;
    [Tooltip("是否让整组商品围绕 Y 轴居中排布。")]
    [SerializeField] private bool centerColumns = true;

    private readonly List<StageShopOfferPoint> _runtimeOfferPoints = new List<StageShopOfferPoint>();

    private void Awake()
    {
        if (offerRoot == null)
            offerRoot = transform;
    }

    private void OnValidate()
    {
        maxColumns = Mathf.Max(1, maxColumns);
        horizontalSpacing = Mathf.Max(0f, horizontalSpacing);
        verticalSpacing = Mathf.Max(0f, verticalSpacing);
    }

    /// <summary>
    /// 按指定商品数量重建商品槽位。
    /// </summary>
    /// <param name="offerCount">商品数量。</param>
    /// <returns>生成后的商品槽位列表。</returns>
    public StageShopOfferPoint[] RebuildOfferPoints(int offerCount)
    {
        int safeOfferCount = Mathf.Max(0, offerCount);
        if (safeOfferCount <= 0 || offerPointPrefab == null)
        {
            ClearOfferPoints();
            return Array.Empty<StageShopOfferPoint>();
        }

        Transform root = offerRoot != null ? offerRoot : transform;
        EnsureOfferPointCount(safeOfferCount, root);
        for (int i = 0; i < _runtimeOfferPoints.Count; i++)
        {
            StageShopOfferPoint offerPoint = _runtimeOfferPoints[i];
            if (offerPoint == null)
                continue;

            offerPoint.transform.localPosition = CalculateLocalPosition(i, safeOfferCount);
            offerPoint.transform.localRotation = Quaternion.identity;
            offerPoint.transform.localScale = Vector3.one;
            offerPoint.name = $"StageShopOfferPoint_{i:00}";
            offerPoint.SetOfferIndex(i);
        }

        return _runtimeOfferPoints.ToArray();
    }

    /// <summary>
    /// 清理当前自动生成的商品槽位。
    /// </summary>
    public void ClearOfferPoints()
    {
        for (int i = _runtimeOfferPoints.Count - 1; i >= 0; i--)
        {
            StageShopOfferPoint offerPoint = _runtimeOfferPoints[i];
            if (offerPoint != null)
                Destroy(offerPoint.gameObject);
        }

        _runtimeOfferPoints.Clear();
    }

    /// <summary>
    /// 计算指定索引商品的本地排布位置。
    /// </summary>
    /// <param name="index">商品索引。</param>
    /// <param name="totalCount">商品总数。</param>
    /// <returns>商品本地位置。</returns>
    private Vector3 CalculateLocalPosition(int index, int totalCount)
    {
        int columns = Mathf.Max(1, maxColumns);
        int row = index / columns;
        int column = index % columns;
        int rowCount = Mathf.CeilToInt(totalCount / (float)columns);
        int currentRowCount = Mathf.Min(columns, totalCount - row * columns);

        float x = column * horizontalSpacing;
        float y = -row * verticalSpacing;
        if (centerRows)
            x -= (currentRowCount - 1) * horizontalSpacing * 0.5f;
        if (centerColumns)
            y += (rowCount - 1) * verticalSpacing * 0.5f;

        return layoutOffset + new Vector3(x, y, 0f);
    }

    /// <summary>
    /// 调整运行时商品点数量，尽量复用已有实例，避免购买刷新时商品表现闪烁。
    /// </summary>
    /// <param name="targetCount">目标商品点数量。</param>
    /// <param name="root">商品点父节点。</param>
    private void EnsureOfferPointCount(int targetCount, Transform root)
    {
        for (int i = _runtimeOfferPoints.Count - 1; i >= targetCount; i--)
        {
            StageShopOfferPoint offerPoint = _runtimeOfferPoints[i];
            if (offerPoint != null)
                Destroy(offerPoint.gameObject);

            _runtimeOfferPoints.RemoveAt(i);
        }

        while (_runtimeOfferPoints.Count < targetCount)
        {
            StageShopOfferPoint offerPoint = Instantiate(offerPointPrefab, root);
            _runtimeOfferPoints.Add(offerPoint);
        }
    }
}
