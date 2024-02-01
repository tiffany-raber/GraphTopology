using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interval : ExperimentTask
{
    [Header("Task-specific Properties")]
    [Tooltip("in miliseconds")]  public int meanInterval = 6000;
    [Tooltip("in miliseconds")] public int plusOrMinus = 0;
    public bool equalIncrements = true;
    public bool shuffle = true;

    private float startTime = -1f;
    private float[] intervalList = new float[0];
    private int currentIntervalIndex;

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

        // If this already ran and we're in the middle of the task, don't re-initialize
        if (intervalList.Length == 0)
        {
            intervalList = new float[parentTask.repeat];

            // Generate Intervals
            // Rough Equavalent of Python's np.linspace or R's seq()
            // Else just randomly pick a number in the inclusive range
            if (equalIncrements)
            {
                var nextValue = (float)meanInterval - plusOrMinus;
                var interval = (float)(meanInterval + plusOrMinus - nextValue) / (intervalList.Length - 1);
                for (int iInt = 0; iInt < intervalList.Length; iInt++)
                {
                    intervalList[iInt] = Mathf.Round(nextValue); // gives us some extra precision while hitting the upper bound of the range
                    nextValue += interval;
                }
            }
            else
            {
                for (int iInt = 0; iInt < intervalList.Length; iInt++)
                {
                    intervalList[iInt] = Random.Range(meanInterval - plusOrMinus, meanInterval - plusOrMinus);
                }
            }

            // Randomize the order if requested
            if (shuffle) Experiment.Shuffle(intervalList);
        }

        hud.SecondsToShow = 0;
    }


    public override bool updateTask()
    {
        if (startTime < 0) startTime = Time.time;
        // Just wait until the specified time has passed
        while (Time.time - startTime < intervalList[currentIntervalIndex] / 1000) // convert to seconds
        {
            return false;
        }
        return true;

    }


    public override void endTask()
    {
        TASK_END();

        // LEAVE BLANK
    }


    public override void TASK_END()
    {
        base.endTask();

        // Log data
        taskLog.AddData(transform.name + "_onset_s", startTime.ToString());
        taskLog.AddData(transform.name + "_duration_s", (intervalList[currentIntervalIndex]/1000).ToString());

        // Housekeeping
        startTime = -1f;
        currentIntervalIndex++;

        hud.SecondsToShow = hud.GeneralDuration;
    }

}