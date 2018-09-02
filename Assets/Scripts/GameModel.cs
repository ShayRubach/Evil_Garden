﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Point {
    public int x { get; set; }
    public int y { get; set; }
}

/* 
 * the Board. it will hold all needed data and calculations
 * for our gameplay and be responsible for the pre-instantiations
 * of the GameObjects.
*/

public class GameModel : MonoBehaviour {

    public delegate void Announcement (SoldierTeam winner);
    public static event Announcement FinishGame;

    private static readonly int COLS = 7;
    private static readonly int ROWS = 6;

    private static readonly string UNITY_OBJECTS_TAG = "UnityObject";

    private static readonly int LEFT_BOARD_EDGE_IDX = 0;
    private static readonly int RIGHT_BOARD_EDGE_IDX = COLS - 1;
    private static readonly int TOP_BOARD_EDGE_IDX = 0;
    private static readonly int BTM_BOARD_EDGE_IDX = ROWS - 1;
    private static readonly float MINIMUM_DRAG_DISTANCE = 40.0f;

    public static readonly string NO_SOLDIER_NAME_VAR = "no_soldier";
    public static readonly string PLAYER_NAME_VAR = "soldier_player";
    public static readonly string ENEMY_NAME_VAR = "soldier_enemy";
    public static readonly string TILE_NAME_VAR = "tile_";
    public static readonly string SPOTLIGHT_NAME_VAR = "spotlight";
    public static readonly string PREVIEW_SOLDIER_NAME_VAR = "preview_soldier_player";
    public static readonly string PATH_INDICATORS_NAME_VAR = "path_indicators";
    public static readonly string LEAF_INDICATOR_NAME_VAR = "leaf";

    public GameObject FocusedEnemy { get; set; }
    public GameObject FocusedPlayer { get; set; }

    private GameObject pathIndicators;
    private Vector3 relativePos;
    private Point nextMoveCoord;

    private Dictionary<string, GameObject> objects = new Dictionary<string, GameObject>();

    private void Awake() {

        GameObject[] objectsArray = GameObject.FindGameObjectsWithTag(UNITY_OBJECTS_TAG);

        foreach (GameObject obj in objectsArray) {
            objects.Add(obj.name, obj);
        }

        pathIndicators = objects[GameModel.PATH_INDICATORS_NAME_VAR];

        nextMoveCoord.x = 0;
        nextMoveCoord.y = 0;

    }
    
    /*
     * returns a reference to a tile by vector pos
     */ 
    public GameObject PointToTile(Vector3 pos) {
        float x=0, y=0;
        x = Mathf.Abs(pos.x);
        y = Mathf.Abs(pos.z);
        
        return objects[TILE_NAME_VAR + x + y];
    }

    public Dictionary<string,GameObject> GetObjects() {
        return objects;
    }

    public GameObject GetObject(string name) {
        return objects[name];
    }

    public MovementDirections GetSoldierMovementDirection(Vector3 startPos, Vector3 endPos) {
        relativePos = endPos - startPos;

        //Debug.Log("distance: " + Vector3.Distance(startPos, endPos));
        if (Vector3.Distance(startPos, endPos) < MINIMUM_DRAG_DISTANCE) {
            return MovementDirections.NONE;
        }
        
        float angle = Mathf.Atan2(-relativePos.y, -relativePos.x) * Mathf.Rad2Deg;
        return CalculateMovementDirectionByAngle(angle);
    }

    private MovementDirections CalculateMovementDirectionByAngle(float angle) {
        MovementDirections movement = MovementDirections.NONE;

        if (angle >= -45.0f && angle <= 45.0f) {
            movement = MovementDirections.LEFT;
        }
        else if (angle >= -135.0f && angle <= -45.0f) {
            movement = MovementDirections.UP;
        }
        else if (angle >= 45.0f && angle <= 135.0f) {
            movement = MovementDirections.DOWN;
        }
        else {
            movement = MovementDirections.RIGHT;
        }


        //Debug.Log("movement direction = " + movement);
        return movement;
    }

    /*
     * this function moves the player to a new desired position and updates
     * both tile and soldier with their new references.
     * pay attention there's the 'focusedSoldierP' which is the actual soldier parent (wrapper in scene)
     * and there's the 'exactSoldierObj' which is the actual soldier GameObject and the 1st child of 'focusedSoldierP'.
     * the parent is used to move all soldier and it's children relatively on board
     */ 
    public void MoveSoldier(GameObject focusedSoldierP, MovementDirections soldierMovementDirection) {
        
        //start as default position just for initialization:
        Vector3 newPosition = focusedSoldierP.transform.position;
        GameObject exactSoldierObj = focusedSoldierP.transform.GetChild(0).gameObject;

        //save reference of curr tile:
        GameObject currTile = exactSoldierObj.GetComponent<SC_Soldier>().Tile;

        switch (soldierMovementDirection) {
            case MovementDirections.UP:
                newPosition = new Vector3(exactSoldierObj.transform.position.x, exactSoldierObj.transform.position.y, exactSoldierObj.transform.position.z + 1);
                break;
            case MovementDirections.DOWN:
                newPosition = new Vector3(exactSoldierObj.transform.position.x, exactSoldierObj.transform.position.y, exactSoldierObj.transform.position.z - 1);
                break;
            case MovementDirections.LEFT:
                newPosition = new Vector3(exactSoldierObj.transform.position.x - 1, exactSoldierObj.transform.position.y, exactSoldierObj.transform.position.z);
                break;
            case MovementDirections.RIGHT:
                newPosition = new Vector3(exactSoldierObj.transform.position.x + 1, exactSoldierObj.transform.position.y, exactSoldierObj.transform.position.z);
                break;
        }

        //get the new tile by new position
        GameObject newTile = PointToTile(newPosition);

        ResetTileReference(currTile);
        UpdateTileAndSoldierRefs(newTile, exactSoldierObj, true, false);

        //physically move the soldier
        exactSoldierObj.transform.position = newPosition;
    }

    private bool ResetTileReference(GameObject tile) {
        return UpdateTileAndSoldierRefs(tile, null, false, true);
    }

    private bool UpdateTileAndSoldierRefs(GameObject tile, GameObject soldier, bool occupied, bool traversal) {
        if(tile == null)
            return false;

        tile.GetComponent<SC_Tile>().soldier = soldier;
        tile.GetComponent<SC_Tile>().IsOcuupied = occupied;
        tile.GetComponent<SC_Tile>().IsTraversal = traversal;

        //if soldier is null, it means we only want to lose reference to a soldier and NOT update new soldier with this current tile.
        //else, we wish to update both tile and soldier with their references.
        if(soldier != null) {
            soldier.GetComponent<SC_Soldier>().Tile = tile;
        }

        return true;
    }

    public void Match() {
        Debug.Log("Starting Match...");

        //call our MatchHandler to evaluate the match result:
        MatchStatus result = MatchHandler.GetInstance.EvaluateMatchResult(FocusedPlayer, FocusedEnemy);
        Debug.Log("match status = " + result);

        HandleMatchResult(result);
    }

    /*
     * take the necessary actions by the result: remove losing soldier, call MoveSoldier() ,update new references etc..
     */ 
    private void HandleMatchResult(MatchStatus result) {
        
        //get the initiator's movement direction:
        MovementDirections direction = CalculateMovementDirectionByAngle(Mathf.Atan2(-relativePos.y, -relativePos.x) * Mathf.Rad2Deg);

        switch (result) {
            case MatchStatus.PLAYER_WON_THE_MATCH:
                MoveSoldier(FocusedPlayer.transform.parent.gameObject, direction);
                goto case MatchStatus.PLAYER_REVEALED;      //c# restrictions: can't fallthrough without the special 'goto case' keyword
            case MatchStatus.PLAYER_REVEALED:
                RemoveSoldier(FocusedEnemy);
                RevealSoldier(FocusedPlayer);
                break;
            case MatchStatus.ENEMY_WON_THE_MATCH:
                MoveSoldier(FocusedEnemy.transform.parent.gameObject, ReverseDirection(direction));
                goto case MatchStatus.ENEMY_REVEALED;       //c# restrictions: can't fallthrough without the special 'goto case' keyword
            case MatchStatus.ENEMY_REVEALED:
                RemoveSoldier(FocusedPlayer);
                RevealSoldier(FocusedEnemy);
                break;
            case MatchStatus.TIE:
                RemoveSoldier(FocusedPlayer);
                RemoveSoldier(FocusedEnemy);
                break;
            case MatchStatus.PLAYER_WON_THE_GAME:
                CallFinishGame(SoldierTeam.PLAYER);
                break;
            case MatchStatus.ENEMY_WON_THE_GAME:
                CallFinishGame(SoldierTeam.ENEMY);
                break;
        }

    }

    private MovementDirections ReverseDirection(MovementDirections direction) {
        switch (direction) {
            case MovementDirections.UP: return MovementDirections.DOWN;
            case MovementDirections.DOWN: return MovementDirections.UP;
            case MovementDirections.RIGHT: return MovementDirections.LEFT;
            case MovementDirections.LEFT: return MovementDirections.RIGHT;
        }
        return MovementDirections.NONE;
    }

    private void RevealSoldier(GameObject soldier) {
        Debug.Log("revealing " + soldier);
    }

    private void RemoveSoldier(GameObject soldier) {
        Debug.Log("removing " + soldier);
    }

    void CallFinishGame(SoldierTeam winner) {
        if (FinishGame != null)
            FinishGame(winner);
    }

    private MatchStatus EvaluateMatchResult() {

        return MatchStatus.TIE;
    }

    public void ShowPathIndicators(Vector3 objectPos) {
        ResetIndicators();                                      //enable and show all indicators.
        pathIndicators.transform.position = objectPos;          //move all indicators so they surround the object.
        FilterIndicators(objectPos);                            //hide non travesal indicators.
        
    }

    private void ResetIndicators() {

        pathIndicators.SetActive(true);

        for (int i = 0; i < pathIndicators.transform.childCount; ++i) {
            pathIndicators.transform.GetChild(i).gameObject.SetActive(true);
        }
    }

    public TileStatus GetNextTileStatus() {
        //calculate new tile requested
        GameObject tile = objects[TILE_NAME_VAR + nextMoveCoord.x + nextMoveCoord.y];

        //save reference to the opponent for easier access later from the controller:
        FocusedEnemy = tile.GetComponent<SC_Tile>().soldier;

        if (FocusedEnemy != null) {
            //next tile is occupied with a soldier
            if(FocusedEnemy.GetComponent<SC_Soldier>().Team == SoldierTeam.ENEMY) {
                
                return TileStatus.VALID_OPPONENT;
            }
            else {
                //soldier from the player's team, non traversal
                return TileStatus.PLAYER_SOLDIER;
            }
        }
        
        return TileStatus.TRV_TILE;
    }

    /*
     * used to decide which indicators (right,left,up,down) indicators are eligible to be displayed
     * according to the position of the soldier
    */
    private void FilterIndicators(Vector3 pos) {
        Vector3 requestedTilePos;

        //soldier is located in most left side of the border
        requestedTilePos = new Vector3(pos.x-1, pos.y, pos.z);
        if (Mathf.Abs(pos.x) == LEFT_BOARD_EDGE_IDX || RequestTileIsOccupied(PointToTile(requestedTilePos))) {
            HideObjectUnderBoard(pathIndicators.transform.GetChild((int)Indicators.LEFT).gameObject);
        }

        //soldier is located in most right side of the border
        requestedTilePos = new Vector3(pos.x + 1, pos.y, pos.z);
        if (Mathf.Abs(pos.x) == RIGHT_BOARD_EDGE_IDX || RequestTileIsOccupied(PointToTile(requestedTilePos))) {
            HideObjectUnderBoard(pathIndicators.transform.GetChild((int)Indicators.RIGHT).gameObject);
        }

        //soldier is located in the top side of the border
        requestedTilePos = new Vector3(pos.x, pos.y, pos.z + 1);
        if (Mathf.Abs(pos.z) == TOP_BOARD_EDGE_IDX || RequestTileIsOccupied(PointToTile(requestedTilePos))) {
            HideObjectUnderBoard(pathIndicators.transform.GetChild((int)Indicators.UP).gameObject);
        }

        //soldier is located in the bottom side of the border
        requestedTilePos = new Vector3(pos.x, pos.y, pos.z - 1);
        if (Mathf.Abs(pos.z) == BTM_BOARD_EDGE_IDX || RequestTileIsOccupied(PointToTile(requestedTilePos))) {
            HideObjectUnderBoard(pathIndicators.transform.GetChild((int)Indicators.DOWN).gameObject);
        }
    }

    private bool RequestTileIsOccupied(GameObject tile) {
        return tile.GetComponent<SC_Tile>().IsOcuupied;
    }

    private void HideObjectUnderBoard(GameObject obj) {
        //obj.transform.position = new Vector3(obj.transform.position.x, obj.transform.position.y - 1, obj.transform.position.z);
        obj.SetActive(false);
    }

    public void HidePathIndicators() {
        HideObjectUnderBoard(pathIndicators);
    }

    public bool IsValidMove(Vector3 soldierPos, MovementDirections move) {
        bool isValid = false;
        nextMoveCoord.x = (int)Mathf.Abs(soldierPos.x);
        nextMoveCoord.y = (int)Mathf.Abs(soldierPos.z);


        //the 'z' axis is treated as 'y' on our board, due to camera placement.
        switch (move) {
            case MovementDirections.UP:
                if (Mathf.Abs(soldierPos.z) - 1 >= TOP_BOARD_EDGE_IDX) {
                    nextMoveCoord.y -= 1;
                    isValid = true;
                }
                break;
            case MovementDirections.DOWN:
                if (Mathf.Abs(soldierPos.z) + 1 <= BTM_BOARD_EDGE_IDX) {
                    nextMoveCoord.y += 1;
                    isValid = true;
                }
                break;
            case MovementDirections.LEFT:
                if (soldierPos.x - 1 >= LEFT_BOARD_EDGE_IDX) {
                    nextMoveCoord.x -= 1;
                    isValid = true;
                }
                break;
            case MovementDirections.RIGHT:
                if (soldierPos.x + 1 <= RIGHT_BOARD_EDGE_IDX) {
                    nextMoveCoord.x += 1;
                    isValid = true;
                }
                break;
        }

        return isValid;
    }

    public Point GetNextMoveCoord() {
        return nextMoveCoord;
    }
}
