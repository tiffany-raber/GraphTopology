/*
    Copyright (C) 2010  Jason Laczko

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using UnityEngine;
using System.Collections;
using System.IO;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Valve.VR.InteractionSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Valve.VR;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using TMPro;
using UnityStandardAssets.Characters.FirstPerson;

public enum EndListMode
{
    Loop,
    End
}

//[SerializeField]
public enum UserInterface
{
    KeyboardMouse,
    KeyboardSingleAxis,
    ViveRoomspace,
    ViveVirtualizer,
    ViveKatwalk
}

public class Experiment : MonoBehaviour
{

    public GameObject availableControllers;
    public UserInterface userInterface = UserInterface.KeyboardMouse;
    public GameObject targetObjects;
    // public bool debugging = false;
    
    [HideInInspector]
    public TaskList tasks;
    [HideInInspector]
    public Config config;
    [HideInInspector]
    public GameObject player;
    [HideInInspector]
    public Camera playerCamera;
    [HideInInspector]
    public Camera overheadCamera;
    [HideInInspector]
    public GameObject scaledPlayer;
    [HideInInspector]
    public GameObject environment;
    [HideInInspector]
    public GameObject scaledEnvironment;
    [HideInInspector]
    public bool usingVR;
    [HideInInspector]
    public dbLog dblog;
    [HideInInspector]
    public long playback_time;
    //[HideInInspector]
    //public LM_TrialLog trialLogger;
    [HideInInspector]
    public string logfile;
    [HideInInspector]
    public string dataPath;
    public GameObject virtualDesert;

    // private bool playback = false;
    private bool pause = true;
    private bool done = false;
    private long now;
    // private Event evt;
    private long playback_start;
    private long playback_offset;
    // private long next_time;
    private string[] next_action;
    private string configfile = "";
    private LM_AzureStorage azureStorage;

    protected GameObject avatar;
    protected AvatarController avatarController;
    protected HUD hud;

    public TextMeshProUGUI trialCounter;

    public System.Random random;
    public bool idAsRandomSeed;

    // -------------------------------------------------------------------------
    // -------------------------- Builtin Methods ------------------------------
    // -------------------------------------------------------------------------

    void Awake()
    {
        // ------------------------------
        // Clean up & Initialize Scene
        // ------------------------------

        // trialLogger = new LM_TrialLog();
        dblog = new dbLog();

        // check if we have any old Landmarks instances from LoadScene.cs and handle them
        GameObject oldInstance = GameObject.Find("OldInstance");
        if (oldInstance != null)
        {
            foreach (var item in oldInstance.transform)
            {
                //Destroy(item); // this tends to break the steamvr skeleton buttons and hand rendermodels
                oldInstance.SetActive(false);
            }
        }

        //since config is a singleton it will be the one created in scene 0 or this scene
        config = Config.Instance;
        // config.Initialize(config);
        // Check the config for issues 
        config.CheckConfig();

        // Control randomness to replicate experience for each subject
        if (idAsRandomSeed)
        {
            random = new System.Random(config.id);
            UnityEngine.Random.InitState(config.id);
        }
        else random = new System.Random();

        // Are we using Microsoft Azure
        azureStorage = FindObjectOfType<LM_AzureStorage>();


        // ------------------------------
        // Set up the Experiment
        // ------------------------------

        // Assign experiment variables based on selected controller
        var lmPlayer = GetController();
        player = lmPlayer.controller;
        avatar = lmPlayer.gameObject;
        playerCamera = lmPlayer.cam;
        hud = lmPlayer.headsUpDisplay;
        usingVR = lmPlayer.usesVR;
        //if (!hud.hudRig.transform.IsChildOf(lmPlayer.transform)) hud.hudRig.transform.SetParent(lmPlayer.transform);

        // Initialize controller properties
        playerCamera.gameObject.AddComponent<AudioListener>();
        playerCamera.enabled = true;
        player.tag = "Player";

        // Deactivate all other controllers
        foreach (Transform child in GameObject.Find("PlayerControllers").transform)
        {
            if (child.name != player.name)
            {
                child.gameObject.SetActive(false);
            }
        }

        hud.ReCenter(avatar.transform);
        hud.showOnlyHUD();

        // Assign other experiment variables
        tasks = GameObject.Find("LM_Timeline").GetComponent<TaskList>();
        overheadCamera = GameObject.Find("OverheadCamera").GetComponent<Camera>();
        environment = GameObject.FindGameObjectWithTag("Environment");
        scaledPlayer = GameObject.Find("SmallScalePlayerController");

        // Initialize other experiment variables
        if (usingVR)
        {
            overheadCamera.stereoTargetEye = StereoTargetEyeMask.Both;
        }
        else
        {
            overheadCamera.stereoTargetEye = StereoTargetEyeMask.None;
        }
        overheadCamera.enabled = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        scaledPlayer.SetActive(false);


        // --------------------------------------
        // Handle Config file & Storage path
        // --------------------------------------

        //when in editor
        if (Application.isEditor)
        {
            // If we are using azure in the editor, save to persistentdatapath for file permission
            if (azureStorage != null)
            {
                if (azureStorage.useInEditor)
                {
                    Debug.Log("SAVING AZURE-EDITOR DATA IN PERSISTENTDATAPATH");
                    dataPath =
                        Application.persistentDataPath + "/" +
                        "editor/data/";
                    logfile =
                        config.levelNames[config.levelNumber] + ".log";
                }
                else
                {
                    Debug.Log("OVERWRITING EXISTING AZURE-EDITOR DATA");
                    dataPath =
                        Directory.GetCurrentDirectory() + "/" +
                        "editor-data/";
                    logfile =
                        "test.log";
                }
            }
            // otherwise just save to the project folder for easy access
            else
            {
                dataPath =
                    Directory.GetCurrentDirectory() + "/" +
                    "editor-data/";
                logfile =
                    "test.log";
            }

        }
        // Otherwise, save data in the persistent data path for file permission (regardless of Azure)
        else
        {
            Debug.Log("SAVING BUILD DATA IN PERSISTENTDATAPATH");
            dataPath =
                Application.persistentDataPath + "/" +
                config.experiment + "/" +
                config.id + "/";
            if (config.appendLogFiles)
            {
                logfile =
                   config.experiment + "_" +
                   config.id + ".log";
            }
            else
            {
                logfile =
                    config.experiment + "_" +
                    config.id + "_" +
                    config.levelNames[config.levelNumber] + "_" +
                    config.conditions[config.levelNumber] + ".log";
            }
        }
        Debug.Log("data will be saved as " + dataPath + logfile);

        configfile =
                dataPath +
                config.filename;


        Debug.Log("!!!!!!!!!!!!\t" + SceneManager.GetActiveScene().name + "\t" + config.levelNames[0]);
        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }
        // Prevent editor log files from appending to a previous session's LM_TaskLog
        // by deleting the directory and recreating, unless we're loading multiple scenes
        // in which case we would want to append to these editor files
        else if (Directory.Exists(dataPath) & Application.isEditor & 
                SceneManager.GetActiveScene().name == config.levelNames[0])
        {
            Debug.Log("OVERWRITING EXISTING EDITOR DATA");
            Directory.Delete(dataPath, recursive:true);
            Directory.CreateDirectory(dataPath);
        }

        if (config.runMode == ConfigRunMode.NEW)
        {
            dblog.logFileName = dataPath + logfile;
            dblog.appendToLog = true; //config.appendLogFiles && config.levelNumber > 0; 
        }
        else if (config.runMode == ConfigRunMode.RESUME)
        {
            dblog.logFileName = dataPath + logfile;
            dblog.appendToLog = true;
        }
        // else if (config.runMode == ConfigRunMode.PLAYBACK)
        // {
        //     CharacterController c = avatar.GetComponent<CharacterController>();
        //     c.detectCollisions = false;
        //     dblog = new dbPlaybackLog(dataPath + logfile);
        // }

        string tsvPath = Application.isEditor ? dataPath + "participants.tsv": dataPath + "../participants.tsv";
        if (Application.isEditor && !File.Exists(tsvPath)) File.Delete(tsvPath); 
        bool firstParticipant = !File.Exists(tsvPath);
        var participantsTSV = new StreamWriter(tsvPath, append:!Application.isEditor);
        if (firstParticipant) participantsTSV.WriteLine("participant_id\tage\tsex\tgroup");
        participantsTSV.WriteLine(config.id.ToString() + "\t" + 
                                  config.age.ToString() + "\t" + 
                                  (config.biosexFemale ? "F" : "M") + "\t" + 
                                  "control");
        participantsTSV.Close();

        dblog.log("EXPERIMENT:\t" + PlayerPrefs.GetString("expID") + "\tSUBJECT:\t" + config.id +
                  "\tSTART_SCENE\t" + config.levelNames[config.levelNumber] + "\tSTART_CONDITION:\t" + config.conditions[config.levelNumber] + "\tUI:\t" + userInterface.ToString(), 1);
    }


    void Start()
    {

        ConfigOverrides.parse(configfile, dblog);
        hud.showFPS = config.showFPS;
        hud.showTimestamp = (config.runMode == ConfigRunMode.PLAYBACK);

        //start experiment
        if (config.runMode != ConfigRunMode.PLAYBACK)
        {
            //Debug.Log("Starting the Experiment");
            tasks.startTask();
        }
        else
        {
            hud.flashStatus("Playback Paused");
            next_action = dblog.NextAction();
            // next_time = Int64.Parse(next_action[0]);
            long tick = DateTime.Now.Ticks;
            playback_start = tick / TimeSpan.TicksPerMillisecond;
            playback_offset = 0;
            now = playback_start;
        }

        // find ScaledEnvironment
        try
        {
            scaledEnvironment = FindObjectOfType<LM_ScaledEnvironment>().transform.gameObject;
            scaledEnvironment.gameObject.SetActive(false);
        }
        catch
        {
            scaledEnvironment = null;
        }
        
        if (trialCounter != null) trialCounter.text = string.Format("{0} / {1}", config.levelNumber+1, config.levelNames.Count);
    }

    async void Update()
    {

        if (!done)
        {
            if (config.runMode != ConfigRunMode.PLAYBACK)
            {

                if (Input.GetKeyDown(KeyCode.T))
                {
                    dblog.log("BOOKMARK t-trigger", 1);
                }

                done = tasks.updateTask();

                // THIS IS WHERE THE EXPERIMENT GET'S SHUT DOWN
                if (done)
                {
                    Cursor.visible = true;
                    await EndScene();
                }
            }
            else
            {
                updatePlayback();
            }
        }
    }


    // -------------------------------------------------------------------------
    // ------------------------ LM-Specific Methods ----------------------------
    // -------------------------------------------------------------------------
    public LM_PlayerController GetController()
    {

        LM_PlayerController lmPlayer;

        // Handle the selected UI enum from the inspector
        if (config.ui != "default")
        {
            switch (config.ui)
            {
                case "KeyboardMouse":
                    userInterface = UserInterface.KeyboardMouse;
                    break;
                case "KeyboardSingleAxis":
                    userInterface = UserInterface.KeyboardSingleAxis;
                    break;
                case "ViveVirtualizer":
                    userInterface = UserInterface.ViveVirtualizer;
                    break;
                case "ViveRoomspace":
                    userInterface = UserInterface.ViveRoomspace;
                    break;
                case "ViveKatwalk":
                    userInterface = UserInterface.ViveKatwalk;
                    break;
                default:
                    userInterface = UserInterface.KeyboardMouse;
                    break;
            }
        }

        // Based on the UserInterface enum that was selected, get the player
        switch (userInterface)
        {
            case UserInterface.KeyboardMouse:
                lmPlayer = GameObject.Find("KeyboardMouseController").GetComponent<LM_PlayerController>();

                break;

            case UserInterface.KeyboardSingleAxis:
                lmPlayer = GameObject.Find("KeyboardSingleAxisController").GetComponent<LM_PlayerController>();

                break;

            case UserInterface.ViveRoomspace:
                lmPlayer = GameObject.Find("ViveRoomspaceController").GetComponent<LM_PlayerController>();

                break;

            case UserInterface.ViveVirtualizer:

                try
                {
                    lmPlayer = GameObject.Find("ViveVirtualizerController").GetComponent<LM_PlayerController>();

                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    Debug.LogWarning("The proprietary ViveVirtualizerController asset cannot be found.\n" +
                        "Are you missing the prefab in your Landmarks project or a reference to the prefab in your scene?");

                    goto default;
                }

            case UserInterface.ViveKatwalk:

                try
                {
                    lmPlayer = GameObject.Find("ViveKatwalkController").GetComponent<LM_PlayerController>();

                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    Debug.LogWarning("The proprietary ViveKatwalkController asset cannot be found.\n" +
                        "Are you missing the prefab in your Landmarks project or a reference to the prefab in your scene?");

                    goto default;
                }

            default:

                lmPlayer = GameObject.Find("KeyboardMouseController").GetComponent<LM_PlayerController>();
                Debug.LogWarning("Falling back to default controller (" + lmPlayer.name + ").");

                break;

        }

        return lmPlayer;

    }


    public void StartPlaying()
    {
        long tick = DateTime.Now.Ticks;
        playback_start = tick / TimeSpan.TicksPerMillisecond;
        playback_offset = 0;
    }


    public void OnControllerColliderHit(GameObject hit)
    {
        if (config.runMode != ConfigRunMode.PLAYBACK)
        {
            tasks.OnControllerColliderHit(hit);
        }
    }


    public static long Now()
    {

        long tick = DateTime.Now.Ticks;
        return tick / TimeSpan.TicksPerMillisecond;
    }


    void updatePlayback()
    {

        long last_now = now;
        long tick = DateTime.Now.Ticks;
        now = tick / TimeSpan.TicksPerMillisecond;

        if (Input.GetButtonDown("PlayPause"))
        {
            pause = !pause;
            hud.flashStatus("Playback Paused");
        }

        if (pause)
        {
            playback_offset -= now - last_now;
        }

        float seek = Input.GetAxis("Horizontal");
        //if (seek != 0.0) {
        if (Input.GetButton("Horizontal"))
        {
            playback_offset += 250;// * Convert.ToInt64(seek);
        }
        playback_time = now - playback_start + playback_offset;
        hud.playback_time = playback_time;

        string[] vec;
        Vector3 vec3;

        while ((!pause || (Mathf.Abs(seek) > Mathf.Epsilon)) && !done && dblog.PlaybackTime() <= playback_time)
        {
            Debug.Log(next_action[2]);

            //try {
            if (next_action[2] == "AVATAR_HPR" || next_action[2] == "AVATAR_POS" || next_action[2] == "AVATAR_STOP")
            {
                vec = next_action[3].Split(new char[] { ',', '(', ')', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                vec3 = new Vector3(float.Parse(vec[0]), float.Parse(vec[1]), +float.Parse(vec[2]));
                Type t = typeof(AvatarController);
                t.InvokeMember(next_action[2], BindingFlags.Default | BindingFlags.InvokeMethod, null, avatarController, new System.Object[] { vec3 });
            }
            else if (next_action[2] == "TASK_ROTATE" || next_action[2] == "TASK_POSITION" || next_action[2] == "TASK_SCALE")
            {

                vec = next_action[5].Split(new char[] { ',', '(', ')', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                vec3 = new Vector3(float.Parse(vec[0]), float.Parse(vec[1]), +float.Parse(vec[2]));

                GameObject taskObject = GameObject.Find(next_action[3]);
                Component script = taskObject.GetComponent(next_action[4]) as Component;
                //Type t = typeof(AvatarController);
                this.GetType().InvokeMember(next_action[2], BindingFlags.Default | BindingFlags.InvokeMethod, null, this, new System.Object[] { taskObject, vec3 });
            }
            else if (next_action[2] == "TASK_ADD")
            {
                GameObject taskObject = GameObject.Find(next_action[3]);
                Component script = taskObject.GetComponent(next_action[4]) as Component;
                //Type t = typeof(AvatarController);
                GameObject secondObject = GameObject.Find(next_action[5]);
                script.GetType().InvokeMember(next_action[2], BindingFlags.Default | BindingFlags.InvokeMethod, null, script, new System.Object[] { secondObject, next_action[6] });
            }
            else if (next_action[2] == "INPUT_EVENT")
            {
                hud.flashStatus("Input: " + next_action[3] + " " + next_action[4]);
            }
            else if (next_action[2] == "SET_SCORE")
            {
                hud.setScore(int.Parse(next_action[3]));

            }
            else if (next_action[2] == "INFO")
            {
                //skip
            }
            else if (next_action[2] == "DATA")
            {
                //skip
            }
            else if (next_action[2] == "BOOKMARK")
            {
                //skip
            }
            else if (next_action[2] == "CONFIG_SET")
            {
                //Debug.Log("CONFIG_SET" );
                ConfigOverrides.set_keyvalue(next_action[3] + "=" + next_action[4], "Config: ", dblog);
            }
            else
            {
                //	Debug.Log("else" );
                GameObject taskObject = GameObject.Find(next_action[3]);
                Component script = taskObject.GetComponent(next_action[4]) as Component;

                //Type t = typeof(AvatarController);
                script.GetType().InvokeMember(next_action[2], BindingFlags.Default | BindingFlags.InvokeMethod, null, script, null);
            }
            //}
            //catch (FormatException)
            //{

            //}
            //catch (IndexOutOfRangeException)
            //{

            //}

            //next_action = dblog.NextAction();
            //if (next_action == null)
            //{
            //    hud.setMessage("Playback Done");
            //    done = true;
            //}

        }
    }


    public void TASK_ROTATE(GameObject go, Vector3 hpr)
    {
        go.transform.localEulerAngles = hpr;
    }

    public void TASK_POSITION(GameObject go, Vector3 pos)
    {
        go.transform.position = pos;
    }


    public void TASK_SCALE(GameObject go, Vector3 scale)
    {
        go.transform.localScale = scale;
    }

    //--------------------------------
    // Utility Methods
    //--------------------------------

    public static void Shuffle<T>(T[] array)
    {
        // var random = new System.Random();
        for (int i = array.Length; i > 1; i--)
        {
            // Pick random element to swap.
            int j = FindObjectOfType<Experiment>().random.Next(i); // 0 <= j <= i-1
                                    // Swap.
            T tmp = array[j];
            array[j] = array[i - 1];
            array[i - 1] = tmp;        }
    }

    //recursive calls
    public static void MoveToLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root) MoveToLayer(child, layer);
    }

	// // Calculate the planar distance between placement and targets (i.e., ignore the y-axis height of the copies)
	// public static float Vector3Distance2D(Vector3 v1, Vector3 v2)
	// {
	// 	return (Mathf.Sqrt(Mathf.Pow(Mathf.Abs(v1.x - v2.x), 2f) + Mathf.Pow(Mathf.Abs(v1.z - v2.z), 2f)));
	// }


    /// <summary>
    /// Calculate the planar angle between two points measured at a third vertex point. Returns [-180:180]
    /// </summary>
    /// <param name="a">The transform of the point at the end of the arm/vector to measure from.</param>
    /// <param name="b">The transform of the point where the two arms/vectors meet.</param>
    /// <param name="to">The transform of the point at the end of the arm/vector to measure to.</param>
    /// <param name="signed">Whether or not to use a signed angle (default is unsigned)</param>
    /// <returns>The measured (signed) angle as a float.</returns>
	public static float CalculateAngleThreePoints(Vector3 pointA, Vector3 pointB, Vector3 pointC, bool signed = false) 
	{
        // convert each Vector3 to a Vector2 by stripping the y-value from the Vector3
        var a  = new Vector2(pointA.x, pointA.z);
        var b = new Vector2(pointB.x, pointB.z);
        var c = new Vector2(pointC.x, pointC.z);
        
        // // Calculate the length of each side of the triangle formed by ABC using the Euclidean distance formula
        // // var ab = Mathf.Sqrt(Mathf.Pow(a.x - b.x, 2) + Mathf.Pow(a.y - b.y, 2));
        // // var bc = Mathf.Sqrt(Mathf.Pow(b.x - c.x, 2) + Mathf.Pow(b.y - c.y, 2));
        // // var ac = Mathf.Sqrt(Mathf.Pow(a.x - c.x, 2) + Mathf.Pow(a.y - c.y, 2));
        // var ba = a - b;
        // var bc = c - b;
        // // // Use the law of cosines
        // // var measurement = Mathf.Rad2Deg * Mathf.Acos((Mathf.Pow(ab, 2) + Mathf.Pow(ac, 2) - Mathf.Pow(bc, 2)) / (2 * ab * bc));
        // var measurement = Mathf.Rad2Deg * (Mathf.Atan2(bc.y, bc.x) - Mathf.Atan2(ba.y, ba.x));

        var measurement = Mathf.Rad2Deg * (Mathf.Atan2(c.y - a.y, c.x - a.x) - Mathf.Atan2(b.y - a.y, b.x - a.x));

        // while (measurement > 180) measurement -= 180;
        // while (measurement < -180) measurement += 180;

        return measurement;
	}

    /// <summary>
    /// Calculate the clockwise angle between vectors extending toward "Unity North" and toward a target from a given origin
    /// </summary>
    /// <param name="origin">The point to measure from</param>
    /// <param name="target">The point to measure to</param>
    /// <returns>(float) The world-space polar angle of a line extending from the origin to the target.</returns>
    public static float MeasureClockwiseGlobalAngle(GameObject origin, GameObject target)
    {
        // Temporarily face the origin at the target and record this as the correct answer, then put it back
        var tmp = origin.transform.rotation;
        origin.transform.LookAt(target.transform);
        var northCwDirection = origin.transform.eulerAngles.y;
        origin.transform.rotation = tmp;
        return northCwDirection;
    }

    public void RestrictMovement(bool canMove, bool canLook)
    {
        avatar.GetComponentInChildren<CharacterController>().enabled = !canMove;

        if (GetComponentInChildren<FirstPersonController>()) GetComponentInChildren<FirstPersonController>().LockRotation(canLook);
        if (GetComponentInChildren<LM_MovementController>()) GetComponentInChildren<LM_MovementController>().enabled = !canLook;
    }
    
    // Turn off all Renderers and Canvases on a gameobject and all of its children
    public void HideRecursive(GameObject obj, bool restore = false)
    {
        if (obj.GetComponentsInChildren<Renderer>().Length > 0)
        {
            var mrs = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer mr in mrs) mr.enabled = restore;
        }
        else Debug.LogError("You are trying to hide or reveal a GameObject with no MeshRenderers");

        if (obj.GetComponentsInChildren<Canvas>().Length > 0)
        {
            var mrs = obj.GetComponentsInChildren<Canvas>();
            foreach (Canvas mr in mrs) mr.enabled = restore;
        }
    }

    // Turn off all Colliders on a gameobject and all of its children
    public void DisableRecursive(GameObject obj, bool restore = false, bool ignoreSelf = false)
    {
        if (obj.GetComponentsInChildren<Collider>().Length > 0)
        {
            var cs = obj.GetComponentsInChildren<Collider>();
            foreach (Collider c in cs)
            {
                if (ignoreSelf && c.transform == obj.transform) continue;
                c.enabled = restore;
            }
        }
        else Debug.LogError("You are trying to disable or re-enable a GameObject with no Colliders");
    }


    //--------------------------------
    // Housekeeping Methods
    //--------------------------------

    async Task EndScene()
    {
        // ---------------------------------------------------------------------
        // Clean up tasks and logging
        // ---------------------------------------------------------------------

        if (config.runMode != ConfigRunMode.PLAYBACK)
        {
            tasks.endTask();
        }

        // Log out any EEG triggers (if available)
        var eeg = FindObjectOfType<BrainAmpManager>();
        if (eeg != null)
        {
            dblog.log(eeg.LogTriggerIndices(), 1);
        }

        // close the logfile
        dblog.close();

        
        // TODO CANDIDATE FOR DEPRECATION
        // // ---------------------------------------------------------------------
        // // Generate a clean .csv file for each task in the experiment
        // // ---------------------------------------------------------------------
        // Debug.Log("Generating secondary log files");
        // try
        // {
        //     // Read in the log file and prepare to parse it with RegEx
        //     var sr = new StreamReader(dataPath + logfile);
        //     var loggedData = await sr.ReadToEndAsync();
        //     sr.Close();

        //     // Find LM logging headers and identify unique tasks in this experiment
        //     Regex pattern = new Regex("LandmarksTrialData:\n.*\n(.*\t)\n");
        //     MatchCollection matches = pattern.Matches(loggedData);
        //     List<string> tasks = new List<string>();
        //     foreach (Match match in matches)
        //     {
        //         GroupCollection groups = match.Groups;
        //         var header = groups[1].Value;
        //         if (!tasks.Contains(header))
        //         {
        //             tasks.Add(header);
        //         }
        //     }

        //     // Extract the data for each unique task and append in a .csv
        //     var taskCount = 0;
        //     foreach (var taskHeader in tasks)
        //     {
        //         // Create the file and add the header line
        //         string filename = "task";
        //         taskCount++;

        //         Regex namePattern = new Regex(taskHeader + "\n([A-z0-9._]*)");
        //         MatchCollection nameMatches = namePattern.Matches(loggedData);
        //         foreach (Match nameMatch in nameMatches)
        //         {
        //             GroupCollection nameGroups = nameMatch.Groups;
        //             Debug.Log(nameGroups[1].Value);
        //             filename = nameGroups[1].Value;
        //         }
        //         //filename = "task_" + taskCount;

        //         // Don't overwrite data unless in Editor or if we are appending multiple log files (set on the config)
        //         if (File.Exists(dataPath + filename + ".csv") & !Application.isEditor)
        //         {
        //             int duplicate = 1;
        //             while (File.Exists(dataPath + filename + "_" + duplicate + ".csv"))
        //             {
        //                 duplicate++;
        //             }
        //             filename += "_" + duplicate;
        //         }
        //         filename += ".csv";

        //         // Create the formatted csv file, if there's more than 1 level, append to existing
        //         StreamWriter sw = new StreamWriter(dataPath + filename, false, System.Text.Encoding.UTF8);

        //         sw.WriteLine(taskHeader.Replace("\t", ",")); // commas for excel

        //         // If using Azure, add these files to the list of files to upload
        //         if (azureStorage != null)
        //         {
        //             azureStorage.additionalSaveFiles.Add(filename);
        //         }

        //         // Extract data and write
        //         Regex DataPattern = new Regex(taskHeader + "\n(.*)\n"); // where is the data?
        //         MatchCollection dataMatches = DataPattern.Matches(loggedData);
        //         foreach (Match dataMatch in dataMatches)
        //         {
        //             GroupCollection dataGroups = dataMatch.Groups;

        //             sw.WriteLine(dataGroups[1].Value.ToString().Replace("\t", ",")); // when writing, use commas for excel
        //         }

        //         // clean up (close this file and get ready for next one)
        //         sw.Close();
        //     }
        // }
        // catch (Exception ex)
        // {
        //     Debug.LogException(ex);
        //     Debug.Log("something went wrong generating CSV data files for individual tasks");
        // }
        // Debug.Log("Clean log files have been generated for each task");

        // // Shut down any LM_TaskLogs
        // foreach (var log in FindObjectsOfType<LM_TaskLog>())
        // {
        //     log.output.Close();
        // }
        // TODO (end)


        // ---------------------------------------------------------------------
        // Upload any files staged for Microsoft Azure
        // ---------------------------------------------------------------------
        if (azureStorage != null)
        {
            if (Application.isEditor & !azureStorage.useInEditor)
            {
                Debug.Log("Not saving to MICROSOFT AZURE because the experiment was run from the editor");
            }
            else
            {
                Debug.Log("trying to use MICROSOFT AZURE");
                await azureStorage.BasicStorageBlockBlobOperationsAsync();
            }
        }


        // ---------------------------------------------------------------------
        // Load the next level/scene/condition or quit
        // ---------------------------------------------------------------------

        // Save scene order/data in the config
        config.Save();

        //increment the level number (accounting for the zero-base compared to a count (starts with 1)
        config.levelNumber++;
        // If there is another level, load it
        if (config.levelNumber < config.levelNames.Count && !config.singleSceneBuild)
        {
            // Load the next Scene
            if (usingVR)
            {
                // Use steam functions to avoid issues w/ framerate drop
                SteamVR_LoadLevel.Begin(config.levelNames[config.levelNumber]);
                
                Destroy(transform.parent.gameObject);
                Debug.Log("Loading new VR scene");
            }
            else
            {
                SceneManager.LoadSceneAsync(config.levelNames[config.levelNumber]); 
                Destroy(transform.parent.gameObject);
                // otherwise, just load the level like usual
            }
        }
        // Otherwise, close down; we're done
        else
        {
            config.DeleteTemporaryProgressData();
            // shut it down
            Debug.Log("Closing the application");
            Application.Quit();
        }
    }


    void OnApplicationQuit()
    {
        Cursor.visible = true;
    }

}
