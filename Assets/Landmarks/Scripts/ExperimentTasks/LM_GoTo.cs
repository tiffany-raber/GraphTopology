/*

    Copyright (C) 2022 Michael J. Starrett Ambrose
    University of California, Irvine
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

public class LM_GoTo : ExperimentTask
{
    [Header("Task-specific Properties")]
    public GameObject arriveAt;
    public Vector3 localOffsetFacing;
    public bool hideEnvironment;
    public float orientThreshold = 15.0f;
    [TextArea] public string readyMessage;
    [TextArea] public string notReadyMessage;
    [Tooltip("Only use if your player will already be at the destination \n" + 
                "I.e, you just want them to orient from their current position")]
    public bool immobilize;
    private Vector3 m_immobilizedAt;

    private ParticleSystem effect;
    private bool atDestination;
    private Collider them;

    private new void Awake()
    {   
        GetComponent<Collider>().enabled = false;
    }

    public override void startTask()
    {
        TASK_START();

        // LEAVE BLANK
    }


    public override void TASK_START()
    {
        if (!manager) Start();
        base.startTask();

        them = avatar.GetComponent<LM_PlayerController>().collisionObject;
        Debug.Log(them.name);

        GetComponent<Collider>().enabled = true;

        if (arriveAt.GetComponentInChildren<ParticleSystem>() != null) effect = arriveAt.GetComponentInChildren<ParticleSystem>();
        arriveAt.SetActive(true); // show the destination if it's hidden
        if (effect != null) effect.Play(true); // start particles if we have them
        hud.ReCenter(arriveAt.transform); // move the HUD to the start location
        hud.SecondsToShow = 0; // don't how it unless they are at the start location
        hud.hudPanel.SetActive(false);
        

        if (hideEnvironment) {
            hud.showOnlyHUD(); 
            manager.environment.transform.Find("filler_props").gameObject.SetActive(false);
            manager.targetObjects.gameObject.SetActive(false);
            manager.virtualDesert.SetActive(true);
        }
        else {
            hud.showEverything();
            manager.environment.transform.Find("filler_props").gameObject.SetActive(true);
        }

        // Toggle the collider on then off in case they were already inside this collider on load (e.g., standing at start when experiment begins)
        GetComponent<Collider>().enabled = false;
        GetComponent<Collider>().enabled = true;

         if (immobilize) m_immobilizedAt = avatar.transform.position;
    }


    public override bool updateTask()
    {
        base.updateTask();
        if (skip)
        {
            log.log("INFO    skip task    " + name, 1);
            return true;
        }

        if (immobilize) avatar.transform.position = m_immobilizedAt;

        // Is the player at and aligned with the destination?
        if (atDestination)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(manager.playerCamera.transform.eulerAngles.y, arriveAt.transform.eulerAngles.y)) < orientThreshold)
            {
                if (hud.GetMessage() == "" || hud.GetMessage() == notReadyMessage)
                {
                    hud.setMessage(readyMessage);
                    hud.hudPanel.SetActive(true);
                    hud.ForceShowMessage();
                }

                // Know when to fold 'em (or end the task)
                if (readyMessage == "")
                {
                    hud.hudPanel.SetActive(false);
                    hud.setMessage("");
                    return true;
                }
                else if (vrEnabled)
                {
                    if (vrInput.TriggerButton.GetStateDown(SteamVR_Input_Sources.Any))
                    {
                        Debug.Log("VR trying to start the task");
                        log.log("INPUT_EVENT    Player Arrived at Destination    1", 1);
                        hud.hudPanel.SetActive(false);
                        hud.setMessage("");
                        return true;
                    }
                } else if (Input.GetButtonDown("Return") | Input.GetKeyDown(KeyCode.Return))
                {
                    if (Input.GetButtonDown("Return") | Input.GetKeyDown(KeyCode.Return))
                    {
                        log.log("INPUT_EVENT    Player Arrived at Destination    1", 1);
                        hud.hudPanel.SetActive(false);
                        hud.setMessage("");
                        return true;
                    }
                }
            } else 
            {
                hud.setMessage(notReadyMessage);
                hud.hudPanel.SetActive(true);
                hud.ForceShowMessage();
            }
        }
        else
        {
            hud.setMessage("");
            hud.hudPanel.SetActive(false);
        }

        return false;
    }


    public override void endTask()
    {
        TASK_END();

        // LEAVE BLANK
    }


    public override void TASK_END()
    {
        base.endTask();

        // WRITE TASK EXIT CODE HERE
        GetComponent<Collider>().enabled = false;
        arriveAt.SetActive(false);
        if (hideEnvironment)
        {
            manager.environment.transform.Find("filler_props").gameObject.SetActive(true);
            manager.targetObjects.gameObject.SetActive(true);
            manager.virtualDesert.SetActive(false);
        }
        hud.hudPanel.SetActive(true);
        hud.setMessage("");
        hud.SecondsToShow = hud.GeneralDuration;
        atDestination = false;
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.name ==
            GameObject.FindWithTag("Player").GetComponentInChildren<LM_PlayerController>().collisionObject.gameObject.name)
        {
            //Debug.Log(collision.name + "is here");
            if (effect != null) effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            atDestination = true;

            if(hud != null) hud.SecondsToShow = 9999999; // keep the hud on
        }
    }

    private void OnTriggerExit(Collider collision)
    {
        if (collision.gameObject.name ==
            GameObject.FindWithTag("Player").GetComponentInChildren<LM_PlayerController>().collisionObject.gameObject.name)
        {
            if (effect != null) effect.Play(true);
            atDestination = false;

            if (hud != null) hud.SecondsToShow = 0; // keep the HUD off
        }
    }

}
