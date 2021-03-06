﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Unity.Collections;
using Unity.Jobs;

public class PixelManager : MonoBehaviour
{
    [Header("Dimensions")]
    public uint totalRow;
    public uint totalCol;
    public float pixelSize;

    public Vector2 topLeftPos;

    public Pixel[,] pixelArray;
    
    [Header("Physics")]
    public float simTimeStep;

    [Header("Tilemap")]
    public Tilemap tilemap;
    public Vector3Int topLeft;
    public TileBase emptyTile;
    public TileBase waterTile;
    public Tilemap foreground;
    public TileBase waterForeground;
    public TileBase electricityTile;

    List<Transform> sharknadoTransforms;

    // Start is called before the first frame update
    void Start()
    {
        sharknadoTransforms = new List<Transform>();
        InitializeGrid();
        StartCoroutine(PhysicsSim());
        RenderGrid();
    }


    void Update() {
        if (Input.GetKeyUp(KeyCode.Space)) {
            Transform t = GameObject.FindWithTag("Player").transform;
            if (sharknadoTransforms.Exists(element => element == t)) {
                StopTornado(t);
            }
            else {
                StartTornado(t);
            }
        }
    }


    public void InitializeGrid() {
        pixelArray = new Pixel[totalRow, totalCol];
        // Initialize pixel position
        for(int i = 0; i < totalRow; ++i) {
            for (int j = 0; j < totalCol; ++j) {
                pixelArray[i, j] = new Pixel();
                pixelArray[i, j].grid = this;
                pixelArray[i, j].row = i;
                pixelArray[i, j].col = j;
                pixelArray[i, j].movedThisFrame = false;
                if (i > totalRow / 2) {
                    pixelArray[i, j].content = PixelContent.Water;
                }
                else if (i == totalRow / 2 && j % 2 == 0) {
                    pixelArray[i, j].content = PixelContent.Water;
                }
                else {
                    pixelArray[i, j].content = PixelContent.Empty;
                }
                
                pixelArray[i, j].dir = PixelDirection.Down;
            }
        }
    }

    void PixelMovement(Pixel px) {
         switch (px.content)
        {
            case PixelContent.Water:
                WaterMovement(px);
                break;
            case PixelContent.Empty:
                break;
        }
    }

    void WaterMovement(Pixel px) {
        // Force
        Pixel pxToSwap = GetPixelToSwap(px, px.dir);
        if (pxToSwap != null) {
            switch (pxToSwap.content)
            {
                case PixelContent.Water:
                    break;                
                default:
                    SwapContent(px, pxToSwap);
                    break;
            }
        }

        if (!px.movedThisFrame) {
            List<Pixel> emptyList = CheckEmpty(px);
            int rand = Random.Range(0, emptyList.Count + 1);
            if (rand != emptyList.Count) {
                SwapContent(px, emptyList[rand]);
            }
        }
    }

    void SwapContent(Pixel px1, Pixel px2) {
        PixelContent tempContent = px1.content;
        px1.content = px2.content;
        px2.content = tempContent;
        RenderPixel(px1);
        RenderPixel(px2);
        px1.movedThisFrame = true;
        px2.movedThisFrame = true;
    }

    #region Display
    public void RenderGrid() {
        foreach (Pixel px in pixelArray)
        {
            RenderPixel(px);
        }
    }

    public void RenderPixel(Pixel px) {
        switch (px.content)
        {
            case PixelContent.Empty:
                tilemap.SetTile(new Vector3Int(topLeft.x + px.col, topLeft.y - px.row, topLeft.z), emptyTile);
                foreground.SetTile(new Vector3Int(topLeft.x + px.col, topLeft.y - px.row, topLeft.z), emptyTile);
                break;      
            case PixelContent.Water:
                tilemap.SetTile(new Vector3Int(topLeft.x + px.col, topLeft.y - px.row, topLeft.z), waterTile);
                foreground.SetTile(new Vector3Int(topLeft.x + px.col, topLeft.y - px.row, topLeft.z), waterForeground);
                break;
            case PixelContent.Electricity:
                tilemap.SetTile(new Vector3Int(topLeft.x + px.col, topLeft.y - px.row, topLeft.z), waterTile);
                tilemap.SetTile(new Vector3Int(topLeft.x + px.col, topLeft.y - px.row, topLeft.z), electricityTile);
                break;
        }
    }
    #endregion


    IEnumerator PhysicsSim() {
        while (true) {
            HandleTornados();
            UpdatePixelPhysics();
            yield return new WaitForSeconds(simTimeStep);
        }
    }

    Pixel GetPixelToSwap(Pixel px, PixelDirection dir) {
        switch (dir)
        {
            case PixelDirection.Up:
                if (px.row > 0) {
                    return(pixelArray[px.row - 1, px.col]);
                }
                break;
            case PixelDirection.Down:
                if (px.row < totalRow - 1) {
                    return(pixelArray[px.row + 1, px.col]);
                }
                break;
            case PixelDirection.Left:
                if (px.col > 0) {
                    return(pixelArray[px.row, px.col - 1]);
                }
                break;
            case PixelDirection.Right:
                if (px.col < totalCol - 1) {
                    return(pixelArray[px.row, px.col + 1]);
                }
                break;
            default:
                break;
        }
        return null;
    }

    List<Pixel> CheckEmpty(Pixel px) {
        List<Pixel> returnList = new List<Pixel>();
        // if (px.row > 0 && pixelArray[px.row - 1, px.col].content == PixelContent.Empty) {
        //     returnList.Add(pixelArray[px.row - 1, px.col]);
        // }
        if (px.row < totalRow - 1 && pixelArray[px.row + 1, px.col].content == PixelContent.Empty) {
            returnList.Add(pixelArray[px.row + 1, px.col]);
        }
        if (px.col > 0 && pixelArray[px.row, px.col - 1].content == PixelContent.Empty) {
            returnList.Add(pixelArray[px.row, px.col - 1]);
        }
        if (px.col < totalCol - 1 && pixelArray[px.row, px.col + 1].content == PixelContent.Empty) {
            returnList.Add(pixelArray[px.row, px.col + 1]);
        }
        return returnList;
    }

    void UpdatePixelPhysics() {
        int rand = Random.Range(0, 4);
        if (rand / 2 == 0) {
            for (uint i = 0; i < totalRow; i++) {
                if (rand % 2 == 0) {
                    for (uint j = 0; j < totalCol; j++) {
                        PixelMovement(pixelArray[i, j]);
                    }
                }
                else {
                    for (uint j = totalCol - 1; j > 0; j--) {
                        PixelMovement(pixelArray[i, j]);
                    }
                }
            }
        }
        else {
            for (uint i = totalRow - 1; i > 0; i--) {
                if (rand % 2 == 0) {
                    for (uint j = 0; j < totalCol; j++) {
                        PixelMovement(pixelArray[i, j]);
                    }
                }
                else {
                    for (uint j = totalCol - 1; j > 0; j--) {
                        PixelMovement(pixelArray[i, j]);
                    }
                }
            }
        }
        foreach (Pixel px in pixelArray) {
            px.movedThisFrame = false;
        }
    }
    public Vector2Int GetPixelPos(Vector3 worldPos) {
        int x = tilemap.WorldToCell(worldPos).x - topLeft.x;
        int y = tilemap.WorldToCell(worldPos).y - topLeft.y;
        return new Vector2Int(x, y);
    }

    public PixelContent GetContentWorld(Vector3 worldPos) {
        if (tilemap.GetTile(tilemap.WorldToCell(worldPos)) == waterTile) {
            return PixelContent.Water;
        }
        else if (tilemap.GetTile(tilemap.WorldToCell(worldPos)) == electricityTile) {
            return PixelContent.Electricity;
        }
        else {
            return PixelContent.Empty;
        }
    }

    public void StartTornado(Transform sharknado) {
        sharknadoTransforms.Add(sharknado);
    }

    public void StopTornado(Transform sharknado) {
        sharknadoTransforms.Remove(sharknado);
    }


    void HandleTornados() {
        foreach (Pixel px in pixelArray) {
            px.dir = PixelDirection.Down;
        }
        foreach (Transform sharknado in sharknadoTransforms) {
            TornadoForceField(sharknado);
        }
    }

    void TornadoForceField(Transform sharknado) {
        // Figure out which column is the shark in
        int sharkCol = GetPixelPos(sharknado.position).x;
        int innerRange = 6;
        int outerRange = 15;
        int inOutCutoff = 10;
        int bottomRow = (int) totalRow ;

        // Middle: Force = Up
        for(int j = sharkCol - innerRange; j <= sharkCol + innerRange; ++j) {
            if (j >= 0 && j < totalCol) {
                for (int i = 0; i < bottomRow; ++i) {
                    pixelArray[i, j].dir = PixelDirection.Up;
                }
            }
        }

        // Left Low: Force = Right
        for(int j = sharkCol - outerRange; j > sharkCol - innerRange; ++j) {
            if (j >= 0 && j < totalCol) {
                for (int i = inOutCutoff; i < bottomRow; ++i) {
                    pixelArray[i, j].dir = PixelDirection.Right;
                }
            }
        }

        // Right Low: Force = Left
        for(int j = sharkCol + innerRange; j < sharkCol + outerRange; ++j) {
            if (j >= 0 && j < totalCol) {
                for (int i = inOutCutoff; i < bottomRow; ++i) {
                    pixelArray[i, j].dir = PixelDirection.Left;
                }
            }
        }

        // Left High: Force = Left
        for(int j = sharkCol - outerRange; j > sharkCol - innerRange; ++j) {
            if (j >= 0 && j < totalCol) {
                for (int i = 0; i <= inOutCutoff; ++i) {
                    pixelArray[i, j].dir = PixelDirection.Left;
                }
            }
        }

        // Right High: Force = Right
        for(int j = sharkCol + innerRange + 1; j < sharkCol + outerRange; ++j) {
            if (j >= 0 && j < totalCol) {
                for (int i = 0; i <= inOutCutoff; ++i) {
                    pixelArray[i, j].dir = PixelDirection.Right;
                }
            }
        }
    }

}