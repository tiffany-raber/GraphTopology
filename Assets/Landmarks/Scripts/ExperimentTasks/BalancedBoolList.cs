﻿using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;


public class BalancedBoolList : ExperimentTask
{
    [Header("Task-specific Properties")]
    public int trialsPerRun = 28;
    public TaskList overrideTrialsPerRun;
    public bool controlStaySwitch;
    public int totalRuns = 8;
    public TaskList overrideTotalRuns;
    [Tooltip("Restrict identical consecutive values, excluding the initial appearance")] 
    public int maxRepeat = 2;
    public List<bool> outTrials = new List<bool>();
    [Tooltip("if [0 1 1]; add [1 0 0]")] 
    public bool includeMirroredRuns;
    

    public override void startTask()
    {
        TASK_START();

        // LEAVE BLANK
    }


    public override void TASK_START()
    {
        if (!manager) Start();
        base.startTask();

        if (skip)
        {
            log.log("INFO    skip task    " + name, 1);
            return;
        }

        var secondOverride = overrideTrialsPerRun.overrideRepeat;
        if (overrideTrialsPerRun != null)
            trialsPerRun = secondOverride == null ? overrideTrialsPerRun.repeat : secondOverride.objects.Count;
        secondOverride = overrideTotalRuns.overrideRepeat;
        if (overrideTotalRuns != null) 
            totalRuns = secondOverride == null ? overrideTotalRuns.repeat : secondOverride.objects.Count;

        if (trialsPerRun % 2 != 0) Debug.LogError("Trials cannot be balanced if they aren't even");

        //order = Enumerable.Range(0, trialsPerRun).Select(x => x % 2).ToList();
        
        List<List<bool>> sequences = new List<List<bool>>();
        var order = Enumerable.Repeat<bool>(true, trialsPerRun / 2).ToList();
        order.AddRange(Enumerable.Repeat<bool>(false, trialsPerRun / 2).ToList());
        do
        {
            // Randomize, then make sure a trial type doesn't appear consecutively more than the max allowed
            order = order.OrderBy(x => manager.random.Next()).ToList();
            // if (sequences.Any(seq => seq.SequenceEqual(order))) continue;
            int repCount = 0;
            int switchCount = 0;
            int maxReps = 0;
            for (int iEntry = 0; iEntry < order.Count; iEntry++)
            {
                if (iEntry > 0)
                {
                    if (order[iEntry] == order[iEntry - 1]) repCount++;
                    else {repCount = 0; switchCount++;}

                    if (repCount > maxReps) maxReps = repCount;
                }
            }

            // Only add the generated sequence if all conditions are met (if not, while-loop will reiterate)
            if (maxReps <= maxRepeat && switchCount == trialsPerRun / 2) 
            {
                sequences.Add(order.ToList());
                // Debug.Log("Sequence #: " + sequences.Count + "\tMaximum repetitions: " + maxReps + 
                //     "\tSwitch Trials: " + switchCount);
            }
            
        } while (sequences.Count < totalRuns / 2);

        // Now that we have our unique blocks, if necessary, alternate between adding an extra 'switch' at the beginning
        // then, if necessary, create a mirror for each sequence 
        List<List<bool>> mirrorSequences = new List<List<bool>>();
        var addswitch = true;
        for (int iSeq = 0; iSeq < sequences.Count; iSeq++)
        {
            if (controlStaySwitch) 
            {
                // Add a switch at the beginning to validate that the first trial wouldn't really be stay or switch
                // even though it's counted as a switch
                sequences[iSeq].Insert(0, !sequences[iSeq][0]);
                // Then add another value to the start, alternating between stay and switch
                if (addswitch) sequences[iSeq].Insert(0, !sequences[iSeq][0]);
                else sequences[iSeq].Insert(0, sequences[iSeq][0]);
                addswitch = !addswitch;
            }
            List<bool> mirror = new List<bool>();
            for (int iTrial = 0; iTrial < sequences[iSeq].Count; iTrial++) mirror.Add(!sequences[iSeq][iTrial]);
            mirrorSequences.Add(mirror);
        }
        if (includeMirroredRuns) sequences.AddRange(mirrorSequences);
       

        // Randomize these pseudo-random, controlled runs and concatenate
        var shuffledList = sequences.OrderBy(_ => manager.random.Next()).ToList();
        //Experiment.Shuffle(sequences.ToArray());

        // Write a csv output for checking trialbalancing
        StreamWriter writer = new StreamWriter(manager.dataPath + "conditions.csv");
        foreach (var run in shuffledList)
        {
            writer.WriteLine(string.Join(",", run));
            // List<string> pRun = new List<string>();
            // foreach (var trial in run) pRun.Add(trial.ToString());
            // printlist.Add(pRun);
        }
        writer.Close();

        // One long list
        foreach (var trial in shuffledList.SelectMany(run => run)) outTrials.Add(trial);
    }


    public override bool updateTask()
    {
        return true;

        // WRITE TASK UPDATE CODE HERE
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
    }
}

