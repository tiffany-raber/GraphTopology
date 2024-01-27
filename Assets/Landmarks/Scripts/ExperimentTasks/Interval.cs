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

    private float startTime;
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
                Debug.Log(interval.ToString());
                for (int iInt = 0; iInt < intervalList.Length; iInt++)
                {
                    intervalList[iInt] = Mathf.Round(10*nextValue)/10; // gives us some extra precision while hitting the upper bound of the range
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
    }


    public override bool updateTask()
    {
        if (startTime == 0f) startTime = Time.time;
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

        // Update the interval for next time
        currentIntervalIndex++;
    }

}