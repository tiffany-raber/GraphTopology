﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading.Tasks;
using TMPro;
using System.IO;
using System;
using System.Linq;

#if WINDOWS_UWP && ENABLE_DOTNET
using Windows.Storage;
#endif

public class LM_ExpStartup : MonoBehaviour
{
    [Header("Config Options")]
    public Config[] configsProvided;

    [Min(0)] [Tooltip(">0: Automatically select ascending from id provided\n" + "0: Manually select with GUI")]
        public int id = 0;
    [Min(0)] private int run = 0;
    //public bool balanceConditionOrder = true;
    [HideInInspector] public bool singleSceneBuild = true; // could be deprecated?????
    [Tooltip("Can use some, all, or none")]
        public GuiElements guiElements;

    // Private Variables
    private List<Config> configs = new List<Config>();
    private Config config;
    private int autoID;
    private List<int> usedIds = new List<int>();
    private bool subidError = true;
    private bool uiError = true;
    private bool biosexError = true;
    private bool ageError = true;
    private bool abortExperiment;
    private string appDir;
    private bool existingData;
    public  List<Selectable> fields;
    int _fieldIndexer;

    private void Awake()
    {
        appDir = Application.persistentDataPath;

        if (id != 0 | guiElements.subID == null)
        {
            // Set a default ID if need be 
            if (guiElements.subID == null)
            {
                Debug.LogError("No field for providing a subject id manually; automatically generating id starting at 1001");
                autoID = 1001;
            }

            // find an id with no data saved (don't overwrite)
            if (!Application.isEditor)
            {
                while (Directory.Exists(appDir + "/" + autoID))
                {
                    autoID++;
                }
            }
            Debug.Log("Participant ID: " + autoID);
            if (guiElements.subID != null) guiElements.subID.gameObject.SetActive(false);

            // set our public ID value that gets fed to Config.instance
            id = autoID;

            gameObject.SetActive(true);
        }
        else
        {
            guiElements.subID.gameObject.SetActive(true);
            gameObject.SetActive(true);
        }
        GetComponentInChildren<Button>().onClick.AddListener(OnStartButtonClicked);

        // Create a dummy config to be replaced by the one provided or the selection dropdown
        var tmp = new GameObject("defaultConfig");
        tmp.AddComponent<Config>();
        config = tmp.GetComponent<Config>();
        if (configsProvided.Length == 1)
        {
            ChangeConfig();
            guiElements.studyCodes.gameObject.SetActive(false);
        }
        else guiElements.studyCodes.gameObject.SetActive(true);
    }

    void Start()
    {
        // Make any saved config prefabs (must be in Assets/**/Resources/Configs)
        // available in the dropdown (universal startup)
        var opts = Resources.LoadAll<Config>("Configs/");
        foreach (Config opt in opts)
        {
            // If specific configs were specified, only add those
            if (configsProvided.Length > 0 && !configsProvided.Contains(opt)) continue;
            // Update the config dropdown to relfect options
            configs.Add(opt);
            var option = new TMP_Dropdown.OptionData();
            option.text = opt.name;
            guiElements.studyCodes.options.Add(option);
        }

        fields = new List<Selectable>()
        {  
            guiElements.subID,
            guiElements.biosex,
            guiElements.age
        };

    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (fields.Count <= _fieldIndexer)
            {
                _fieldIndexer = 0;
            }
            fields[_fieldIndexer].Select();
            _fieldIndexer++;
        }

        if (!abortExperiment & Input.GetKeyDown(KeyCode.Return))
        {
            try
            {
                SceneManager.LoadScene(config.levelNames[config.levelNumber]);
            }
            catch (System.Exception)
            {
                Application.Quit();
            }
        }
    }

    public void OnStartButtonClicked()
    {
        TextMeshProUGUI startErrorMessage = guiElements.start.transform.Find("Error").GetComponent<TextMeshProUGUI>();

        Debug.Log("trying to load experiment");

        // check manual id if provided

        ValidateSubjectID(); 
            
        if (guiElements.biosex != null) ValidateBiosex();
        if (guiElements.age != null) ValidateAge();

        ValidateUI();

        if (!subidError && !uiError && !biosexError && !ageError)
        {
            // Create the directories if they don't exist
            if (!Directory.Exists(appDir + "/" + config.id.ToString()))
            {
                Directory.CreateDirectory(appDir + "/" + config.id.ToString());

                if (!Directory.Exists(appDir + "/" + config.id.ToString() + "/" + config.experiment))
                {
                    Directory.CreateDirectory(appDir + "/" + config.id.ToString() + "/" + config.experiment);
                }
            }

            readyConfig();
            
            try
            {
                SceneManager.LoadScene(config.levelNames[config.levelNumber]);
            }
            catch (System.Exception)
            {
                Debug.LogError("No scene was found... Check the build settings.");
                throw;
            }
        }
        else
        {
            Debug.Log("found an error still...");
            startErrorMessage.gameObject.SetActive(true);
        }
    }

    void readyConfig()
    {
        config = Config.Instance;
       
        config.runMode = ConfigRunMode.NEW;
        config.bootstrapped = true;
        config.appPath = appDir;
        config.id = id;
        if (guiElements.biosex != null) config.biosexFemale = guiElements.biosex.captionText.text == "Female";
        if (guiElements.age != null) config.age = int.Parse(guiElements.age.text);
        config.run = run;
        config.ui = guiElements.ui.options[guiElements.ui.value].text;
        if (existingData) config.Load();
        //config.level = config.levelNames[0];

        config.CheckConfig();
        DontDestroyOnLoad(config);
    }

    public void ValidateUI()
    {
        TextMeshProUGUI _errorMessage = guiElements.ui.transform.Find("Error").GetComponent<TextMeshProUGUI>();
        if (guiElements.ui.value != 0)
        {
            uiError = false;
            _errorMessage.gameObject.SetActive(false);
        }
        else
        {
            uiError = true;
            _errorMessage.text = "Please select a valid UI.";
            _errorMessage.gameObject.SetActive(true);
        }
    }

    void ValidateBiosex()
    {
        TextMeshProUGUI _errorMesage = guiElements.biosex.transform.Find("Error").GetComponent<TextMeshProUGUI>();
        if (guiElements.biosex.value != 0)
        {
            biosexError = false;
            _errorMesage.gameObject.SetActive(false);
        }
        else
        {
            biosexError = true;
            // _errorMesage.text = "[Text you'd like here]";
            _errorMesage.gameObject.SetActive(true);
        }
    }

    void ValidateAge()
    {
        TextMeshProUGUI _errorMessage = guiElements.age.transform.Find("Error").GetComponent<TextMeshProUGUI>();
        if (guiElements.age.text != "")
        {
            ageError = false;
            _errorMessage.gameObject.SetActive(false);
        }
        else
        {
            ageError = true;
            // _errorMesage.text = "[Text you'd like here]";
            _errorMessage.gameObject.SetActive(true);
        }
    }

    public void ValidateSubjectID()
    {
        Debug.Log("checking the subject id");

        TextMeshProUGUI _errorMessage = guiElements.subID.transform.Find("Error").GetComponent<TextMeshProUGUI>();

        // check if a subID was even provided
        if (guiElements.subID.text != "")
        {
            // if so, make sure it's an int
            if (int.TryParse(guiElements.subID.text, out int _subID))
            {
                // If this id has already been used to save data, flag an error
                if (!Application.isEditor &
                    Directory.Exists(appDir + "/" + guiElements.subID.text + "/" + config.experiment))
                {
                    if (File.Exists(appDir + "/" + guiElements.subID.text + "/" + config.experiment + "/progress.dat"))
                    {
                        subidError = false;
                        existingData = true;
                        id = int.Parse(guiElements.subID.text);
                        run = int.Parse(guiElements.runNum.text);
                        
                        _errorMessage.text = "Loading SubjectID data from a previous session.";
                        _errorMessage.gameObject.SetActive(true);
                    }
                    else
                    {
                        subidError = true;
                        _errorMessage.text = "That SubjectID is already in use.";
                        _errorMessage.gameObject.SetActive(true);
                    }
                }
                else
                {
                    id = int.Parse(guiElements.subID.text);
                    if (guiElements.runNum != null && guiElements.runNum.gameObject.activeSelf) run = int.Parse(guiElements.runNum.text);
                    subidError = false;
                    _errorMessage.gameObject.SetActive(false); // then and only then, will we release the flag
                }
            }
            // if the subID is not an int, throw the message to fix
            else
            {
                subidError = true;
                _errorMessage.text = "Subject ID must be an integer.";
                _errorMessage.gameObject.SetActive(true);
            }
        }
        else
        {
            subidError = true;
            _errorMessage.text = "You must provide a Subject ID.";
            _errorMessage.gameObject.SetActive(true);
        }
    }

    public void ChangeConfig()
    {
        
        if (config != null) Destroy(config.gameObject);

        if (configsProvided.Length == 1)
        {
            config = Instantiate(configsProvided[0]);
        }
        try {config = Instantiate(configs[guiElements.studyCodes.value - 1]);}
        catch (SystemException) { } 
    }
}

[System.Serializable]
public class GuiElements
{
    public TMP_Dropdown studyCodes;
    public TMP_InputField subID;
    public TMP_InputField runNum;
    public TMP_Dropdown biosex;
    public TMP_InputField age;
    public TMP_Dropdown ui;
    public TMP_Dropdown condition;
    public TMP_Dropdown environment;
    public Button start;
    public Toggle practice;
}
