using System;
using System.Collections.Generic;
using UnityEngine;
// AI https://share.gemini.google/VYVGuzcYhaHV
// AI 2: https://share.gemini.google/0ZSCmXAsuGQ7\

public class MouseHighlightManager : MonoBehaviour
{
    [Header("Highlight Settings")]
    public Color highlightColor = new Color(1f, 0.8f, 0f, 1f);
    public float highlightLineWidth = 0.12f;
    public float highlightHeightOffset = 0.1f;

    private HexGrid grid;
    private Camera mainCamera;
    private WorldObjectToCard currentHoveredPin;
    
    private List<LineRenderer> highlightLines = new List<LineRenderer>();
    private Material highlightMaterial;

    private void Start()
    {
        GameObject hexManager = GameObject.FindGameObjectWithTag("HexManager");
        if (hexManager != null)
        {
            grid = hexManager.GetComponent<HexGrid>();
        }

        mainCamera = Camera.main;
        highlightMaterial = new Material(Shader.Find("Sprites/Default"));
    }

    private void Update()
    {
        if (mainCamera == null || grid == null) return;
        
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane gridPlane = new Plane(Vector3.up, grid.transform.position);
        WorldObjectToCard pin = null;

        if (gridPlane.Raycast(ray, out float hitDistance))
        {
            Vector3 hitPoint = ray.GetPoint(hitDistance);
            HexCell nearestCell = grid.GetNearestCell(hitPoint);

            if (nearestCell != null && Vector3.Distance(hitPoint, nearestCell.center) <= grid.hexSize)
            {
                if (nearestCell.occupant != null)
                {
                    PinBase hoveredPinBase = nearestCell.occupant.GetComponentInParent<PinBase>();
                    if (hoveredPinBase == null)
                    {
                        hoveredPinBase = nearestCell.occupant.GetComponent<PinBase>();
                    }

                    if (hoveredPinBase != null && hoveredPinBase.allowPinSelection)
                    {
                        pin = nearestCell.occupant.GetComponentInParent<WorldObjectToCard>();
                        
                        if (pin == null)
                        {
                            pin = nearestCell.occupant.GetComponent<WorldObjectToCard>();
                        }
                    }
                }
            }
        }

        if (pin != null)
        {
            if (pin != currentHoveredPin)
            {
                currentHoveredPin = pin;
                UpdateHighlights();
            }
        }
        else if (currentHoveredPin != null)
        {
            currentHoveredPin = null;
            ClearHighlights();
        }
    }

    private void UpdateHighlights()
    {
        ClearHighlights();

        if (currentHoveredPin == null || currentHoveredPin.GroupPeers == null) return;

        foreach (WorldObjectToCard peer in currentHoveredPin.GroupPeers)
        {
            if (peer == null) continue;

            HexCell cell = grid.GetNearestCell(peer.transform.position);
            if (cell != null)
            {
                CreateHighlightHex(cell.center);
            }
        }
    }

    private void CreateHighlightHex(Vector3 center)
    {
        GameObject go = new GameObject("HoverHighlightHex");
        go.transform.SetParent(transform);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = 6;
        lr.widthMultiplier = highlightLineWidth;
        lr.material = highlightMaterial;
        lr.startColor = highlightColor;
        lr.endColor = highlightColor;

        Vector3[] verts = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float rad = Mathf.Deg2Rad * (30f + 60f * i);
            verts[i] = center + new Vector3(grid.hexSize * Mathf.Cos(rad), highlightHeightOffset, grid.hexSize * Mathf.Sin(rad));
        }

        lr.SetPositions(verts);
        highlightLines.Add(lr);
    }

    private void ClearHighlights()
    {
        foreach (var line in highlightLines)
        {
            if (line != null)
            {
                Destroy(line.gameObject);
            }
        }
        highlightLines.Clear();
    }
}