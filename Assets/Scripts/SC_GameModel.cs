﻿using System;
using System.Collections;
using System.Collections.Generic;
using com.shephertz.app42.gaming.multiplayer.client.events;
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

public class SC_GameModel : MonoBehaviour {

    public delegate void Announcement (SoldierTeam winner);
    public static event Announcement FinishGame;
    public static event Announcement OnMatchFinished;

    public delegate void NotifyToController();
    public static event NotifyToController AIMoveFinished;
    public static event NotifyToController OnMatchStarted;
    public static event NotifyToController OnSoldierMovementComplete;
    public static event NotifyToController CallTieBreaker;
    public static event NotifyToController Shuffling;



    private bool playingVsAI = true;
    public bool iPickedNewWeapon { get; set; }
    public bool rivalPickedNewWeapon { get; set; }
    private const int COLS = 7;
    private const int ROWS = 6;
    private const int MIRROR_OFFSET = 1;

    //private static readonly int MAX_SOLDIER_PER_TEAM = 14;

    public static readonly int REVEAL_SPOTLIGHT_CHILD_IDX = 1;

    private static readonly string UNITY_OBJECTS_TAG = "UnityObject";
    private static readonly int LEFT_BOARD_EDGE_IDX = 0;
    private static readonly int RIGHT_BOARD_EDGE_IDX = COLS - 1;
    private static readonly int TOP_BOARD_EDGE_IDX = 0;
    private static readonly int BTM_BOARD_EDGE_IDX = ROWS - 1;
    private static readonly int LEGAL_MOVES_COUNT = 4;
    private static readonly float MINIMUM_DRAG_DISTANCE = 40.0f;
    private static readonly float THINKING_TIME_IN_SECONDS = 1.0f;

    public static readonly string GAME_MODEL_NAME_VAR = "SC_GameModel";
    public static readonly string NO_SOLDIER_NAME_VAR = "no_soldier";
    public static readonly string PLAYER_NAME_VAR = "soldier_player";
    public static readonly string ENEMY_NAME_VAR = "soldier_enemy";
    public static readonly string TILE_NAME_VAR = "tile_";
    public static readonly string SPOTLIGHT_NAME_VAR = "spotlight";
    public static readonly string PREVIEW_SOLDIER_NAME_VAR = "preview_player";
    public static readonly string PATH_INDICATORS_NAME_VAR = "path_indicators";
    public static readonly string LEAF_INDICATOR_NAME_VAR = "leaf";
    public static readonly string TIE_WEAPONS_P_VAR_NAME = "tie_weapon_options";
    public static readonly string END_GAME_OPTIONS_VAR_NAME = "end_game_options";
    public static readonly string BATTLE_ANIMATOR_VAR_NAME = "battle_animations";
    public static readonly string CRYSTAL_VAR_NAME = "Crystal";
    public static readonly string ANNOUNCER_VAR_NAME = "announcer";
    public static readonly string BG_MUSIC_GAMEPLAY_VAR_NAME = "bg_music_gameplay";
    public static readonly string PLAYER_MOVE_VAR_NAME = "move_self";
    public static readonly string ENEMY_MOVE_VAR_NAME = "move_enemy";
    public static readonly string SHUFFLE_VAR_NAME = "shuffle";



    private static readonly string DATA_KEY_VAR_NAME = "Data";
    private static readonly string PLAYER_TURN_INDICATOR_VAR_NAME = "player_turn_indicator";
    private static readonly string ENEMY_TURN_INDICATOR_VAR_NAME = "enemy_turn_indicator";
    private static readonly string SHUFFLE_HANDLER_VAR_NAME = "shuffle_handler";

    public static readonly string PREVIEW_ANIMATION_TRIGGER_PREFIX = "Preview";
    public static readonly string ANNOUNCER_WIN_TRIGGER = "Win";
    public static readonly string ANNOUNCER_LOSE_TRIGGER = "Lose";
    public static readonly string ANNOUNCER_TIE_TRIGGER = "Tie";
    public static readonly string ANNOUNCER_VICTORY_TRIGGER = "Victory";
    public static readonly string ANNOUNCER_DEFEAT_TRIGGER = "Defeat";
    public static readonly string END_GAME_TRIGGER = "GameFinished";
    public static readonly string SHUFFLE_TRIGGER = "Shuffle";

    private const string ACTION_MOVE = "M";
    private const string ACTION_MOVE_UP = "MU";
    private const string ACTION_MOVE_DOWN = "MD";
    private const string ACTION_MOVE_LEFT = "ML";
    private const string ACTION_MOVE_RIGHT = "MR";
    private const string ACTION_SHUFFLE = "S";
    private const string ACTION_TIE = "T";

    public GameObject FocusedEnemy { get; set; }
    public GameObject FocusedPlayer { get; set; }
    public GameObject board;
    public MovementDirections nextMovement;

    private GameObject pathIndicators;
    internal GameObject playerTurnIndicator, enemyTurnIndicator;
    private Vector3 relativePos;
    private Point nextMoveCoord;
    private SoldierTeam winningTeam;

    Dictionary<string, object> sentData = new Dictionary<string, object>();
    Dictionary<string, object> receivedData = new Dictionary<string, object>();

    List<GameObject> players = new List<GameObject>();
    List<GameObject> enemies = new List<GameObject>();
    List<GameObject> tiles = new List<GameObject>();

    private Dictionary<string, GameObject> objects = new Dictionary<string, GameObject>();

    private void Awake() {
        CreateObjectsDictionary();
        AssignGameObjectsReference();
        InitSpecialGameObjects();
    }

    private void CreateObjectsDictionary() {

        GameObject[] objectsArray = GameObject.FindGameObjectsWithTag(UNITY_OBJECTS_TAG);

        foreach (GameObject obj in objectsArray) {
            try {
                objects.Add(obj.name, obj);

                //save these objects for a later use such as in restart game (optimization)
                if (obj.name.Contains(PLAYER_NAME_VAR))
                    players.Add(obj);
                if (obj.name.Contains(ENEMY_NAME_VAR))
                    enemies.Add(obj);
                if (obj.name.Contains(TILE_NAME_VAR))
                    tiles.Add(obj);
            }
            catch (ArgumentException e) {
                Debug.Log("there's already " + obj.name + " in the dictionary!" + e.ToString());
            }
        }
    }

    private void InitSpecialGameObjects() {
        objects[TIE_WEAPONS_P_VAR_NAME].SetActive(false);
        objects[PLAYER_TURN_INDICATOR_VAR_NAME].SetActive(false);
        objects[ENEMY_TURN_INDICATOR_VAR_NAME].SetActive(false);

        SetStartingPlayer(SharedDataHandler.isPlayerStarting);
        ResetWeaponPicksStatus();

        nextMoveCoord.x = 0;
        nextMoveCoord.y = 0;
    }

    private void AssignGameObjectsReference() {
        pathIndicators = objects[PATH_INDICATORS_NAME_VAR];
        playerTurnIndicator = objects[PLAYER_TURN_INDICATOR_VAR_NAME];
        enemyTurnIndicator = objects[ENEMY_TURN_INDICATOR_VAR_NAME];
    }

    internal void ResetWeaponPicksStatus() {
        iPickedNewWeapon = false;
        rivalPickedNewWeapon = false;
    }

    internal bool BothPlayersPickedWeapon() {
        return (iPickedNewWeapon && rivalPickedNewWeapon);
    }

    private void SortList(List<GameObject> list) {
        list.Sort((x, y) => string.Compare(x.name, y.name));
    }

    internal void SetStartingPlayer(bool isPlayerStarting) {
        playerTurnIndicator.SetActive(isPlayerStarting);
        enemyTurnIndicator.SetActive(!isPlayerStarting);
    }

    public void ShuffleTeam(SoldierTeam team) {

        List <GameObject> soldiers = (team == SoldierTeam.PLAYER) ? players : enemies;
        int startingTileIdx = (team == SoldierTeam.PLAYER) ? board.transform.childCount - soldiers.Count : 0;

        //shuffle (internaly) the soldiers list:
        soldiers.Sort((x, y) => UnityEngine.Random.value < 0.5f ? -1 : 1);
        UpdateShuffledPositions(soldiers, startingTileIdx);

        if (SharedDataHandler.isMultiplayer) {
            string data = ACTION_SHUFFLE + FormAShuffledSoldiersIndexList(soldiers);
            SendShuffleAck(data);
        }
    }

    private string FormAShuffledSoldiersIndexList(List<GameObject> soldiers) {
        string data = null;

        foreach (var soldier in soldiers) {
            data += GetActiveWeaponAsJsonCode(soldier.GetComponent<SC_Soldier>().GetActiveWeapon());
        }

        //mirrored string for easier assignment on the other device:
        return ReverseString(data);
    }

    private string ReverseString(string data) {
        char[] dataCharArray = data.ToCharArray();
        Array.Reverse(dataCharArray);
        return new string(dataCharArray);
    }

    private string GetActiveWeaponAsJsonCode(GameObject weapon) {

        string weaponJsonCode = null;

        if (!weapon) {
            Debug.Log("Weapon group is null.");
            return null;
        }
            
        if (weapon.name.Contains(SoldierType.PITCHFORK.ToString().ToLower())) {
            weaponJsonCode = ((int)SoldierType.PITCHFORK).ToString();
        }
        else if (weapon.name.Contains(SoldierType.AXE.ToString().ToLower())) {
            weaponJsonCode = ((int)SoldierType.AXE).ToString();
        }
        else if (weapon.name.Contains(SoldierType.CLUB.ToString().ToLower())) {
            weaponJsonCode = ((int)SoldierType.CLUB).ToString();
        }
        else if (weapon.name.Contains(SoldierType.SHIELD.ToString().ToLower())) {
            weaponJsonCode = ((int)SoldierType.SHIELD).ToString();
        }
        else if (weapon.name.Contains(SoldierType.CRYSTAL.ToString().ToLower())) {
            weaponJsonCode = ((int)SoldierType.CRYSTAL).ToString();
        }

        return weaponJsonCode;
    }

    public void UpdateShuffledPositions(string data) {
        int soldiersCount = data.Length;
        int weaponKeyCode = 0;
        GameObject tile, soldier;

        //iterate over by children order in scene and assign new weapons to their soldiers according
        //to the data received from shuffle ack:
        for (int i = 0; i < soldiersCount ; i++) {
            tile = board.transform.GetChild(i).gameObject;
            soldier = tile.GetComponent<SC_Tile>().soldier;
            weaponKeyCode = (int)Char.GetNumericValue(data[i]);
            soldier.GetComponent<SC_Soldier>().RefreshWeapon((SoldierType)weaponKeyCode);
        }
    }

    private void UpdateShuffledPositions(List<GameObject> soldierList, int startingTileIdx) {

        Vector3 newPos;
        GameObject tile;

        if(Shuffling != null) {
            Shuffling();
        }
        for (int i = startingTileIdx, j = 0; j < soldierList.Count; i++, j++) {
            tile = board.transform.GetChild(i).gameObject;

            //save reference to old position, and then modify x and z (displayed as our 'y') values according to new tile.
            newPos = soldierList[j].transform.position;

            UpdateTileAndSoldierRefs(tile, soldierList[j], true, false);

            //move the soldier to his new tile
            newPos[0] = soldierList[j].GetComponent<SC_Soldier>().Tile.transform.position.x;      //x value
            newPos[2] = soldierList[j].GetComponent<SC_Soldier>().Tile.transform.position.z;      //z value (our 'y')

            soldierList[j].transform.position = newPos;
        }
    }

    internal bool HandleMsgAck(MoveEvent move) {
        
        if (move.getSender() != SharedDataHandler.username) {
            //Debug.Log("HandleMsgAck: sender=" + move.getSender() + ", data=" + move.getMoveData() + ", nextTurn=" + move.getNextTurn());
            if (move.getMoveData() != null) {
                receivedData.Clear();
                receivedData = MiniJSON.Json.Deserialize(move.getMoveData()) as Dictionary<string, object>;
                if (receivedData != null) {
                    ParseJsonData(receivedData[DATA_KEY_VAR_NAME].ToString());
                }
            }
            else {
                Debug.Log("data is null!");
                SharedDataHandler.client.stopGame();
            }
                
        }

        //returns whether its our turn or rival's:
        return (move.getNextTurn() == SharedDataHandler.username);
    }

    internal void HandlePrivateMsgAck(string msg) {
        //Debug.Log("HandlePrivateMsgAck: data = " + msg);
        if (msg != null) {
            ParseJsonData(msg);
        }
    }

    private void ParseJsonData(string data) {
        switch (data[0].ToString()) {
            case ACTION_MOVE:
                HandleMovementAck(data);
                break;
            case ACTION_SHUFFLE:
                HandleShuffleAck(data);
                break;
            case ACTION_TIE:
                HandleTieAck(data);
                break;
            default:
                break;
        }
    }

    private void HandleTieAck(string data) {
        const int NEW_WEAPON_KEY_CODE_IDX = 1;
        SoldierType newWeapon = (SoldierType)(int.Parse(data.Substring(NEW_WEAPON_KEY_CODE_IDX)));
        //FocusedEnemy.GetComponent<SC_Soldier>().Type = newWeapon;
        FocusedEnemy.GetComponent<SC_Soldier>().RefreshWeapon(newWeapon, false);
        rivalPickedNewWeapon = true;

        if (iPickedNewWeapon) {
            //Debug.Log("HandleTieAck: both players picked new weapons. invoking Match()");
            DisplayTurnIndicator();
            ResetWeaponPicksStatus();
            Match(false);
        }
        else {
            //Debug.LogError("i didnt choose a weapon just yet..");
        }
    }

    internal void HandleShuffleAck(string data) {
        //Debug.Log("HandleEnemyShuffle: data = " + data);
        const int SOLDIER_TYPES_STARTING_IDX = 1;
        
        //cut the ACTION_SHUFFLE string identifier, send raw data:
        UpdateShuffledPositions(data.Substring(SOLDIER_TYPES_STARTING_IDX));
    }

    private void HandleMovementAck(string data) {
        //Debug.Log("HandleEnemyMovement: called.");
        string tile = JsonToTileIndex(data);
        MovementDirections direction = JsonToMovementDirection(data);
        FocusedPlayer = objects[TILE_NAME_VAR + tile].GetComponent<SC_Tile>().soldier;

        PerformMove(FocusedPlayer.transform.parent.gameObject, FocusedPlayer.transform.position, direction, false);
    }

    private string JsonToTileIndex(string data) {
        const int TILE_IDX_START = 2;
        const int TILE_IDX_STR_LEN = 2;
        string fixedTileStr = data.Substring(TILE_IDX_START, TILE_IDX_STR_LEN);
        //Debug.Log("JsonToTileIndex: fixedTileStr = " + fixedTileStr);

        return fixedTileStr;
    }

    private MovementDirections JsonToMovementDirection(string data) {
        const int ACTION_MOVE_IDX_START = 0;
        const int ACTION_MOVE_STR_LEN = 2;

        switch (data.Substring(ACTION_MOVE_IDX_START, ACTION_MOVE_STR_LEN)) {
            case ACTION_MOVE_UP: return MovementDirections.UP;
            case ACTION_MOVE_DOWN: return MovementDirections.DOWN;
            case ACTION_MOVE_LEFT: return MovementDirections.LEFT;
            case ACTION_MOVE_RIGHT: return MovementDirections.RIGHT;
            default: return MovementDirections.NONE;
        }
    }

    internal void PlayAsAI() {
        //Debug.Log("Playing as AI");
        
        FocusedPlayer = ChooseValidRandomSoldier();
        StartCoroutine(MoveAsAIWithThinkingTime(THINKING_TIME_IN_SECONDS));
    }

    internal string GetCurrentBattleAnimationParameters() {
        //get current weapons of the player and enemy in our current battle:
        string playerWeapon = FocusedPlayer.GetComponent<SC_Soldier>().Type.ToString();
        string enemyWeapon = FocusedEnemy.GetComponent<SC_Soldier>().Type.ToString();
        
        //fix their naming to be first uppercase letter (to match the animation trigger param names):
        string fixedPlayerWeaponParamName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(playerWeapon.ToString().ToLower());
        string fixedEnemyWeaponParamName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(enemyWeapon.ToString().ToLower());

        //the focused player is set by the initiating team, so if the AI was the initiator, 
        //we make sure to swap parameter order for a mirror view of the animationg:
        if (FocusedPlayer.GetComponent<SC_Soldier>().Team == SoldierTeam.PLAYER)
            return fixedPlayerWeaponParamName + fixedEnemyWeaponParamName;
        else
            return fixedEnemyWeaponParamName + fixedPlayerWeaponParamName;

    }

    private IEnumerator MoveAsAIWithThinkingTime(float waitTime) {
        yield return new WaitForSeconds(waitTime);

        //todo: refactor this part to user PerformMove() func instead
        if (IsPossibleMatch(nextMoveCoord)) {
            Match();
        }
        else {
            MoveSoldier(FocusedPlayer.transform.parent.gameObject, nextMovement);
        }
        if(AIMoveFinished != null)
            AIMoveFinished();

    }

    public bool IsPossibleMatch(Point move) {
        return GetNextTileStatus() == TileStatus.VALID_OPPONENT;
    }

    private GameObject ChooseValidRandomSoldier() {

        System.Random rand = new System.Random();
        int MAX_ATTEMPTS = 100;
        int randomSoldier = 0;
        FocusedPlayer = null;
        MovementDirections movement = MovementDirections.NONE;

        for (int i = 0; i < MAX_ATTEMPTS; i++) {
            randomSoldier = rand.Next(0, enemies.Count);
            FocusedPlayer = enemies[randomSoldier];

            //make sure soldier is alive and in scene:
            if (FocusedPlayer.GetComponent<SC_Soldier>().Alive == false)
                continue;

            movement = GetAvailableMove();

            if (movement != MovementDirections.NONE) {
                break;
            }
        }
    

        if (FocusedPlayer != null) {
            //save next movement for later use:
            nextMovement = movement;
        }
        return FocusedPlayer;
    }

    internal void RestartGame() {

        foreach (GameObject player in players)
            player.GetComponent<SC_Soldier>().Init();
        foreach (GameObject enemy in enemies)
            enemy.GetComponent<SC_Soldier>().Init();
        foreach (GameObject tile in tiles)
            tile.GetComponent<SC_Tile>().Init();

        //winner starts the next match
        SharedDataHandler.isPlayerStarting = (winningTeam == SoldierTeam.PLAYER);
        InitSpecialGameObjects();
    }

    private MovementDirections GetAvailableMove() {
        MovementDirections[] moves = { MovementDirections.UP, MovementDirections.DOWN, MovementDirections.LEFT, MovementDirections.RIGHT };
        System.Random rand = new System.Random();
        int MAX_ATTEMPTS = 20;
        int randomMove = 0;

        for(int i=0; i < MAX_ATTEMPTS; i++) { 
            randomMove = rand.Next(0, LEGAL_MOVES_COUNT);
            if(IsValidMove(FocusedPlayer.transform.position, moves[randomMove])) {
                return moves[randomMove];
            }
        }
        
        return MovementDirections.NONE;
    }

    /*
     * returns a reference to a tile by vector pos
     */
    public GameObject PointToTile(Vector3 pos) {
        float x=0, y=0;

        x = Mathf.Abs(pos.x);
        y = Mathf.Abs(pos.z);

        try {
            return objects[TILE_NAME_VAR + x + y];
        }
        catch(KeyNotFoundException e) {
            Debug.Log("Tile not found = " + TILE_NAME_VAR + x + y + " " + e.ToString());
        }
        return null;    
    }

    public Dictionary<string,GameObject> GetObjects() {
        return objects;
    }

    internal bool PerformMove(GameObject focusedPlayerParent, Vector3 exactSoldierPosition, MovementDirections soldierMovementDirection, bool sendAck = true) {

        bool rc = true;

        if (soldierMovementDirection != MovementDirections.NONE) {
            
            //only move if its within board borders:
            if (IsValidMove(exactSoldierPosition, soldierMovementDirection)) {

                nextMovement = soldierMovementDirection;
                //check if next movement will initiate a fight (as landing on a rival tile): 
                if (IsPossibleMatch(GetNextMoveCoord())) {
                    Match();
                }
                else {
                    //currrently a static movement, will turn into animation later.
                    MoveSoldier(focusedPlayerParent, soldierMovementDirection, sendAck);
                }
                //isMyTurn = false;
                rc = false;
            }
        }

        return rc;
    }

    private void SendAckIfNeeded(Vector3 exactSoldierPosition, MovementDirections soldierMovementDirection) {
        //only send ack if match was initiated by the user:
        if (MatchInitiatedByMe() && SharedDataHandler.isMultiplayer) {
            FixCoordAndSendMovementAck(soldierMovementDirection, exactSoldierPosition);
        }
    }

    private void FixCoordAndSendMovementAck(MovementDirections soldierMovementDirection, Vector3 exactSoldierPosition) {
        int x = (int)(Math.Round(exactSoldierPosition.x));
        int y = (int)(Math.Round(Mathf.Abs(exactSoldierPosition.z)));
        SendMovementAck(soldierMovementDirection, x, y);
    }

    internal bool MatchInitiatedByMe() {
        //if the turn is passed to the enemy, it means we initiated the fight.
        return enemyTurnIndicator.activeSelf;
    }

    public GameObject GetObject(string name) {
        return objects[name];
    }

    public MovementDirections GetSoldierMovementDirection(Vector3 startPos, Vector3 endPos) {
        relativePos = endPos - startPos;

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
        return movement;
    }

    /// <summary>
    ///      this function moves the player to a new desired position and updates. 
    ///      both tile and soldier with their new references.
    /// </summary>
    /// <param name="focusedSoldierP">the soldier parent (wrapper in scene)</param>
    /// <param name="soldierMovementDirection">the actual soldier GameObject and the 1st child of 'focusedSoldierP'</param>
    /// <param name="sendAck">should user send ack to the rival on this movement. false when invoked from HandleMovementAck()</param>
    public void MoveSoldier(GameObject focusedSoldierP, MovementDirections soldierMovementDirection, bool sendAck = true) {
        //start as default position just for initialization:
        Vector3 newPosition = focusedSoldierP.transform.position;
        GameObject exactSoldierObj = focusedSoldierP.transform.GetChild(0).gameObject;

        //save reference of curr tile:
        GameObject currTile = exactSoldierObj.GetComponent<SC_Soldier>().Tile;

        if(focusedSoldierP == null) {
            Debug.Log("MoveSoldier: focusedSoldierP is null.");
            return;
        }
        
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

        if (SharedDataHandler.isMultiplayer && sendAck) {
            FixCoordAndSendMovementAck(soldierMovementDirection, exactSoldierObj.transform.position);
        }

        //physically move the soldier
        //exactSoldierObj.transform.position = newPosition;
        exactSoldierObj.GetComponent<SC_Soldier>().NewFixedPosition = newPosition;
        exactSoldierObj.GetComponent<SC_Soldier>().StartMovingAnimation(soldierMovementDirection);

        if (OnSoldierMovementComplete != null) {
            OnSoldierMovementComplete();
        }

        DisplayTurnIndicator();
    }

    internal void DisplayTurnIndicator() {
        objects[PLAYER_TURN_INDICATOR_VAR_NAME].SetActive(!objects[PLAYER_TURN_INDICATOR_VAR_NAME].activeSelf);
        objects[ENEMY_TURN_INDICATOR_VAR_NAME].SetActive(!objects[ENEMY_TURN_INDICATOR_VAR_NAME].activeSelf);
    }

    private string GetMirroredDirection(MovementDirections direction) {
        switch (direction) {
            case MovementDirections.UP:     return ACTION_MOVE_DOWN;
            case MovementDirections.DOWN:   return ACTION_MOVE_UP;
            case MovementDirections.LEFT:   return ACTION_MOVE_RIGHT;
            case MovementDirections.RIGHT:  return ACTION_MOVE_LEFT;
        }
        return null;
    }

    private void SendMovementAck(MovementDirections direction, int x, int y) {
        string action = GetMirroredDirection(direction);
        string mirroredTile = GetMirroredPosition(x, y);
        SendDataToServer(action + mirroredTile);
    }

    internal void SendTieAck(string tieJson) {
        //Debug.Log("SendTieAck");
        SharedDataHandler.client.sendPrivateChat(SharedDataHandler.enemyUsername, ACTION_TIE + tieJson);
    }

    private void SendShuffleAck(string shuffleJson) {
        //Debug.Log("SendShuffleAck");
        SharedDataHandler.client.sendPrivateChat(SharedDataHandler.enemyUsername, shuffleJson);
    }

    private void SendDataToServer(string data) {
        sentData.Clear();
        sentData.Add(SharedDataHandler.userNameKey, SharedDataHandler.username);
        sentData.Add(SharedDataHandler.dataKey, data);
        string dataJson = MiniJSON.Json.Serialize(sentData);
        SharedDataHandler.client.sendMove(dataJson);
    }


    internal string GetPreviewAnimationTriggerByWeapon(SoldierType weapon) {

        //make the weapon string have first upper case letter to avoid naming conflicts with unity:
        string fixedWeaponStr = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(weapon.ToString().ToLower());
        return PREVIEW_ANIMATION_TRIGGER_PREFIX + fixedWeaponStr;
    }

    private bool ResetTileReference(GameObject tile) {
        return UpdateTileAndSoldierRefs(tile, null, false, true);
    }

    private bool UpdateTileAndSoldierRefs(GameObject tile, GameObject soldier, bool occupied, bool traversal) {
        if(tile == null) {
            Debug.Log("tile is null");
            return false;
        }

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

    public void Match(bool sendAck = true) {

        if (OnMatchStarted != null) {
            OnMatchStarted();
        }

        FixFocus();
        MatchStatus result = MatchHandler.GetInstance.EvaluateMatchResult(FocusedPlayer, FocusedEnemy);
        HandleMatchResult(result);

        DisplayTurnIndicator();

        if (sendAck)
            SendAckIfNeeded(FocusedPlayer.transform.position, nextMovement);
    }

    /*
     * in contrary to playing vs AI, on multiplayer there are further uses of Focused<Type> so it has
     * to match the local logic where Player = local user amd Enemy = remote user.
    */
    private void FixFocus() {
        if (!SharedDataHandler.isMultiplayer || FocusedPlayer.GetComponent<SC_Soldier>().Team == SoldierTeam.PLAYER) {
            return;
        }
        else {
            GameObject temp = FocusedPlayer;
            FocusedPlayer = FocusedEnemy;
            FocusedEnemy = temp;
        }
    }

    /*
     * take the necessary actions by the result: remove losing soldier, call MoveSoldier() ,update new references etc..
     */
    private void HandleMatchResult(MatchStatus result) {

        //get the initiator's movement direction:
        //MovementDirections direction = CalculateMovementDirectionByAngle(Mathf.Atan2(-relativePos.y, -relativePos.x) * Mathf.Rad2Deg);

        Debug.Log("Match: after evaluate, before ack");
        Debug.Log("player=" + FocusedPlayer.GetComponent<SC_Soldier>().Type + ", active weapon=" + FocusedPlayer.GetComponent<SC_Soldier>().GetActiveWeapon().name);
        Debug.Log("enemy=" + FocusedEnemy.GetComponent<SC_Soldier>().Type + ", active weapon=" + FocusedEnemy.GetComponent<SC_Soldier>().GetActiveWeapon().name);

        switch (result) {
        case MatchStatus.INITIATOR_WON_THE_MATCH:
        case MatchStatus.INITIATOR_REVEALED:
            RemoveSoldier(FocusedEnemy);
            RevealSoldier(FocusedPlayer);
            AnnounceMatchWinner(FocusedPlayer.GetComponent<SC_Soldier>().Team);
            break;
        case MatchStatus.VICTIM_WON_THE_MATCH:
        case MatchStatus.VICTIM_REVEALED:
            RemoveSoldier(FocusedPlayer);
            RevealSoldier(FocusedEnemy);
            AnnounceMatchWinner(FocusedEnemy.GetComponent<SC_Soldier>().Team);
            break;
        case MatchStatus.BOTH_LOST_MATCH:
            RemoveSoldier(FocusedPlayer);
            RemoveSoldier(FocusedEnemy);
            AnnounceMatchWinner(SoldierTeam.NO_TEAM);
            break;
        case MatchStatus.TIE:
            TieBreaker();
            break;
        case MatchStatus.INITIATOR_WON_THE_GAME:
            CallFinishGame(FocusedPlayer);
            break;
        case MatchStatus.VICTIM_WON_THE_GAME:
            CallFinishGame(FocusedEnemy);   
            break;
        }

    }

    private void AnnounceMatchWinner(SoldierTeam team) {
        if(OnMatchFinished != null) {
            OnMatchFinished(team);
        }
    }

    private void TieBreaker() {
        if (playingVsAI) {
            SoldierType newWeapon = GetRandomWeapon();
            GetAISoldier().GetComponent<SC_Soldier>().RefreshWeapon(newWeapon);
        }

        if (CallTieBreaker != null)
            CallTieBreaker();
    }

    private GameObject GetAISoldier() {
        return FocusedEnemy.GetComponent<SC_Soldier>().Team == SoldierTeam.ENEMY ? FocusedEnemy : FocusedPlayer;
    }

    public GameObject GetPlayerSoldier() {
        return FocusedPlayer.GetComponent<SC_Soldier>().Team == SoldierTeam.PLAYER? FocusedPlayer : FocusedEnemy;
    }

    private SoldierType GetRandomWeapon() {
        SoldierType[] weapons = { SoldierType.AXE, SoldierType.CLUB, SoldierType.PITCHFORK };
        int rand = new System.Random().Next(0, weapons.Length);
        return weapons[rand];
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
        if (soldier.GetComponent<SC_Soldier>().Team == SoldierTeam.PLAYER)
            TurnOnRevealSpotlight(soldier);
        else
            soldier.GetComponent<SC_Soldier>().RevealWeapon();
    }

    private void TurnOnRevealSpotlight(GameObject soldier) {
        soldier.transform.GetChild(REVEAL_SPOTLIGHT_CHILD_IDX).gameObject.SetActive(true);
    }

    private void HideSoldier(GameObject soldier) {
        return;
    }

    private void RemoveSoldier(GameObject soldier) {
        ResetTileReference(soldier.GetComponent<SC_Soldier>().Tile);
        soldier.GetComponent<SC_Soldier>().Alive = false;
        soldier.SetActive(false);
    }

    void CallFinishGame(GameObject winner) {
        winningTeam = winner.GetComponent<SC_Soldier>().Team;

        if (FinishGame != null)
            FinishGame(winningTeam);
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
            if(FocusedEnemy.GetComponent<SC_Soldier>().Team != FocusedPlayer.GetComponent<SC_Soldier>().Team) {   
                return TileStatus.VALID_OPPONENT;
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

        //fixed int conversions
        int x = (int)(Math.Round(pos.x));
        int y = (int)(Math.Round(pos.y));
        int z = (int)(Math.Round(Mathf.Abs(pos.z)));


        //soldier is located in most left side of the border
        requestedTilePos = new Vector3(x-1, y, z);
        if (x == LEFT_BOARD_EDGE_IDX || RequestTileIsOccupied(PointToTile(requestedTilePos))) {
            HideObjectUnderBoard(pathIndicators.transform.GetChild((int)Indicators.LEFT).gameObject);
        }

        //soldier is located in most right side of the border
        requestedTilePos = new Vector3(x+1, y, z);
        if (x == RIGHT_BOARD_EDGE_IDX || RequestTileIsOccupied(PointToTile(requestedTilePos))) {
            HideObjectUnderBoard(pathIndicators.transform.GetChild((int)Indicators.RIGHT).gameObject);
        }

        //soldier is located in the top side of the border
        requestedTilePos = new Vector3(x, y, z-1);
        if (z == TOP_BOARD_EDGE_IDX || RequestTileIsOccupied(PointToTile(requestedTilePos))) {
            HideObjectUnderBoard(pathIndicators.transform.GetChild((int)Indicators.UP).gameObject);
        }

        //soldier is located in the bottom side of the border
        requestedTilePos = new Vector3(x, y, z+1);
        if (z == BTM_BOARD_EDGE_IDX || RequestTileIsOccupied(PointToTile(requestedTilePos))) {
            HideObjectUnderBoard(pathIndicators.transform.GetChild((int)Indicators.DOWN).gameObject);
        }
    }

    private bool RequestTileIsOccupied(GameObject tile) {
        return tile ? tile.GetComponent<SC_Tile>().IsOcuupied : true;
    }

    private void HideObjectUnderBoard(GameObject obj) {
        obj.SetActive(false);
    }

    public void HidePathIndicators() {
        HideObjectUnderBoard(pathIndicators);
    }

    public bool IsValidMove(Vector3 soldierPos, MovementDirections move) {

        bool isValid = false;
        nextMoveCoord.x = (int)(Math.Round(soldierPos.x)); ;
        nextMoveCoord.y = (int)(Math.Round(Mathf.Abs(soldierPos.z)));


        //the 'z' axis is treated as 'y' on our board, due to camera placement.
        switch (move) {
            case MovementDirections.UP:
                if (Mathf.Abs(soldierPos.z) - 1 >= TOP_BOARD_EDGE_IDX) {
                    nextMoveCoord.y -= 1;
                    if(!OverlayingTeamMember(nextMoveCoord))
                        isValid = true;
                }
                break;
            case MovementDirections.DOWN:
                if (Mathf.Abs(soldierPos.z) + 1 <= BTM_BOARD_EDGE_IDX) {
                    nextMoveCoord.y += 1;
                    if (!OverlayingTeamMember(nextMoveCoord))
                        isValid = true;
                }
                break;
            case MovementDirections.LEFT:
                if (soldierPos.x - 1 >= LEFT_BOARD_EDGE_IDX) {
                    nextMoveCoord.x -= 1;
                    if (!OverlayingTeamMember(nextMoveCoord))
                        isValid = true;
                }
                break;
            case MovementDirections.RIGHT:
                if (soldierPos.x + 1 <= RIGHT_BOARD_EDGE_IDX) {
                    nextMoveCoord.x += 1;
                    if (!OverlayingTeamMember(nextMoveCoord))
                        isValid = true;
                }
                break;
        }

        return isValid;
    }

    private bool OverlayingTeamMember(Point moveCoord) {
        SC_Tile nextTile = objects[TILE_NAME_VAR + moveCoord.x + moveCoord.y].GetComponent<SC_Tile>();


        if (nextTile.IsOcuupied) {
            //Debug.Log("OverlayingTeamMember: nextTile.soldier.GetComponent<SC_Soldier>() = " + nextTile.soldier.GetComponent<SC_Soldier>());
            //Debug.Log("OverlayingTeamMember: FocusedPlayer.GetComponent<SC_Soldier>()    = " + FocusedPlayer.GetComponent<SC_Soldier>());
            //if the next tile has one of our team members, restirct movement:
            if(nextTile.soldier.GetComponent<SC_Soldier>().Team == FocusedPlayer.GetComponent<SC_Soldier>().Team) {
                return true;
            }
        }
        return false;
    }

    public Point GetNextMoveCoord() {
        return nextMoveCoord;
    }

    private string GetMirroredPosition(int x, int y) {
        string mirroredX, mirroredY;
        mirroredX = (COLS - x - MIRROR_OFFSET).ToString();
        mirroredY = (ROWS - y - MIRROR_OFFSET).ToString();
        return mirroredX+mirroredY;
    }

    public void GodMode(bool state) {
        foreach(KeyValuePair<string,GameObject> soldier in objects) {
            if (soldier.Value.name.Contains(ENEMY_NAME_VAR)) {
                if (!state)
                    RevealSoldier(soldier.Value);
                else
                    HideSoldier(soldier.Value);
            }
        }
        
    }
}
