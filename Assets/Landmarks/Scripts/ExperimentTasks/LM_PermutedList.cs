﻿/*
    LM_PermutedList

    Generate all possible permutations (of a specified length) of a list of
    GameObjects without resampling any object in a permutation (e.g., 336
    permutations of 3 GameObjects from a list of 8)

    Copyright (C) 2020 Michael J. Starrett

    Navigate by StarrLite (Powered by Landmarks)
    Human Spatial Cognition Laboratory
    Department of Psychology - University of Arizona   
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum ShuffleMethod
{
    none, 
    random,
    linked,
    sortByFirstList
}

public class LM_PermutedList : ExperimentTask
{
    [Header("Task-specific Properties")]
    public ObjectList[] inputLists;
    [Tooltip("Leave objects as 'None' to automatically generate child ObjectLists.\n" +
        "Assign pre-configured ObjectLists for greater control (recommended).")]
    public List<ObjectList> outputLists = new List<ObjectList>();
    private int subset;
    public int iterations = 1;
    public bool shuffle = true;
    [Tooltip("none:\tAB-AC-BA-BC-CA-CB\n" +
             "random:\tBC-AC-AD-CB-BA-CA\n" +
             "linked:\tCA-AB-BA-AC-CB-BC\n" +
             "sortByFirstList:\tCB-CA-AB-AC-BA-BC")]
    public ShuffleMethod shuffleMethod = ShuffleMethod.none;
    [Tooltip("Sort such that the last item of n-1 is the first item of n")] public bool link = false;
    private static bool linked = false;
    public EndListMode endListBehavior;
    readonly public List<GameObject> currentItem;
    [Tooltip("Use experiment run to index where to start in this list")]
	public bool configControlCurrent;
	public TaskList taskListControlCurrent;
    private int currentIndex;

    //[HideInInspector]
    public List<List<GameObject>> permutedList = new List<List<GameObject>>();

    public override void startTask()
    {
        TASK_START();
    }


    public override void TASK_START()
    {
        if (!manager) Start();
        base.startTask();

        // Deal with the kind of list we are using
        if (inputLists.Length == 1)
        {
            subset = outputLists.Count;
            // Generate permutations
            var listToPermute = inputLists[0];
            permutedList = Permute(listToPermute.objects, subset).ToList();
        }
        else if (inputLists.Length > 1)
        {
            subset = inputLists.Length;
            
            for (int item = 0; item < inputLists[0].objects.Count; item++)
            {
                var setList = new List<GameObject>();
                foreach (var set in inputLists) setList.Add(set.objects[item]);
                permutedList.Add(setList);
            }
        }

        // Note, permuted list will later be transposed to create a
        // single list for each subset requested
        Debug.Log(
            permutedList.Count.ToString() +
            " lists of " +
            permutedList[0].Count.ToString() +
            " objects, each, were generated"
            );


        // Shuffle if necessary
        switch (shuffleMethod)
        {
            case ShuffleMethod.none:
                break;
            case ShuffleMethod.random:
                var pll = new List<List<List<GameObject>>>();
                for (int i = 0; i < iterations; i++)
                {
                    var spl = permutedList;
                    FisherYatesShuffle(spl);
                    pll.Add(spl);
                }
                permutedList = pll.SelectMany(x => x).ToList();
                break;
            case ShuffleMethod.linked:
                // FIXME add code to handle multiple iterations
                permutedList = SortForLinking(permutedList);
                Debug.Log("Linked list contains " + permutedList.Count + "sets");
                break;
            case ShuffleMethod.sortByFirstList:
                // FIXME add code to handle multiple iterations
                // Shuffle the list and extract the unique objects from the first subset list in the shuffled order
                FisherYatesShuffle(permutedList);
                var firstListNames = new List<GameObject>();
                foreach (var sublist in permutedList) firstListNames.Add(sublist[0]);
                var firstListOrder = firstListNames.Distinct().ToList();
                // Go through each unique object in first subset list and find the index of all appearances
                var newIndices = new List<int>();
                foreach (var thing in firstListOrder)
                {
                    var results = Enumerable.Range(0, firstListNames.Count).Where(i => firstListNames[i] == thing).ToList();
                    foreach (var result in results) newIndices.Add(result);
                }
                // Re-sort the permuted list based on the new, grouped indices we just created
                var tmp = newIndices.Select(index => permutedList[index]).ToList();
                permutedList.Clear();
                foreach (var itm in tmp) permutedList.Add(itm);
                break;
            default:
                break;
        }

        // Create/Configure human-readable ObjectLists in the Hierarchy/Inspector
        for (int i = 0; i < subset; i++)
        {
            // Generate a new child ObjectList if not pre-specified in outputLists
            if (outputLists[i] == null)
            {
                var ol = new GameObject();
                ol.AddComponent<ObjectList>();
                var thing = Instantiate(ol, transform);
                thing.name = this.name + "_subset" + i;
                outputLists[i] = thing.GetComponent<ObjectList>();
                Destroy(ol);
            }
            // Populate output list with items from the permuted list (if any items already exist, these will be appended)
            foreach (var entry in permutedList) outputLists[i].objects.Add(entry[i]);

            // Other things to initialize our objectlists
            if (configControlCurrent && taskListControlCurrent != null && manager.config.run > 1)
            {
                outputLists[i].configControlCurrent = configControlCurrent;
                outputLists[i].taskListControlCurrent = taskListControlCurrent;
                outputLists[i].current = (manager.config.run - 1) * taskListControlCurrent.repeat;
            } 
        }
    }


    public override bool updateTask()
    {
        return true;
    }


    public override void endTask()
    {
        TASK_END();
    }


    public override void TASK_END()
    {
        base.endTask();
        skip = true; // do not remake the list on a second run (block)
    }



//------------------------------------------------------------------------------


    // -------------------------------------------------------------------------
    // Factorization Helper ----------------------------------------------------
    // -------------------------------------------------------------------------


    // calculate the factorial of an integer (3! = 1*2*3 = 6)
    public int Factorize(int number)
    {
        int currentTotal = number;

        while (number > 1)
        {
            currentTotal *= number - 1;
            number--;
        }
        return currentTotal;
    }



    // -------------------------------------------------------------------------
    // Permutation Helpers -----------------------------------------------------
    // -------------------------------------------------------------------------

    // iterators to return permutations - www.interact-sw.co.uk/iangblog/2004/09/16/permuterate
    public static List<List<T>> Permute<T>(List<T> list, int count)
    {
        var output = new List<List<T>>();
        if (count == 0)
        {
            var entry = new T[0].ToList();
            output.Add(entry);
        }
        else
        {
            int startingElementIndex = 0;
            var entry = new List<T>();
            foreach (T startingElement in list)
            {
                List<T> remainingItems = AllExcept(list, startingElementIndex).ToList();

                foreach (List<T> permutationOfRemainder in Permute(remainingItems, count - 1))
                {
                    var catIn1 = new T[] { startingElement }.ToList();
                    var catIn2 = permutationOfRemainder.ToList();

                    entry = Concat<T>(catIn1, catIn2);
                    output.Add(entry);
                }
                startingElementIndex += 1;

            }

        }

        return output;
    }

    // Enumerates over contents of both lists.
    public static List<T> Concat<T>(List<T> a, List<T> b)
    {
        var output = new List<T>();
        foreach (T item in a)
        {

            output.Add(item);
        }
        foreach (T item in b)
        {
            output.Add(item);
        }
        return output;
    }

    // Enumerates over all items in the input, skipping over the item
    // with the specified offset.
    public static IEnumerable<T> AllExcept<T>(IEnumerable<T> input, int indexToSkip)
    {
        int index = 0;
        foreach (T item in input)
        {
            if (index != indexToSkip) yield return item;
            index += 1;
        }
    }
    // www.interact-sw.co.uk/iangblog/2004/09/16/permuterate



    // -------------------------------------------------------------------------
    // Shuffle Helper ----------------------------------------------------------
    // -------------------------------------------------------------------------

    public static void FisherYatesShuffle<T>(IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

public static List<List<GameObject>> SortForLinking(List<List<GameObject>> sacredList)
    {
        
        var outList = new List<List<GameObject>>(); ;
        do
        {
            try
            {
                outList.Clear();
                linked = true;
                var unsortedList = sacredList;
                

                int r = unsortedList[0].Count; // How many samples per permutation?
                GameObject lastEnd = null;

                while (unsortedList.Count > 0)
                {
                    var iItem = 0;
                    var thisStart = unsortedList[iItem][0];
                    if (lastEnd != null)
                    {
                        while (thisStart.name != lastEnd.name)
                        {
                            iItem++;
                            thisStart = unsortedList[iItem][0];
                        }
                    }

                    // Remember where we ended
                    lastEnd = unsortedList[iItem][r - 1];
                    outList.Add(unsortedList[iItem]);
                    Debug.Log(unsortedList[iItem][0].name + "----------------->" + unsortedList[iItem][r - 1].name);
                    unsortedList.Remove(unsortedList[iItem]);
                }
            }
            catch (System.Exception)
            {
                linked = false;
                Debug.LogWarning("Hit dead end; re linking permuted list");
               
            }
        } while (!linked);
        Debug.LogWarning("OutList Contains " + outList.Count);
        return outList;
    }


    // -------------------------------------------------------------------------
    // Indexing and Incrementing -----------------------------------------------
    // -------------------------------------------------------------------------

    public List<GameObject> GetCurrentSubset()
    {
        if (currentIndex >= permutedList.Count)
        {
            currentIndex = 0; // reset
            return null;
            
        }
        else
        {
            return permutedList[currentIndex];
        }
    }

    public void IncrementCurrentSubset()
    {
        currentIndex++;
        if (currentIndex >= permutedList.Count)
        {
            switch (endListBehavior)
            {
                case EndListMode.Loop:
                    currentIndex = 0; // start over from the beginning of the list
                    break;

                case EndListMode.End:
                    Debug.LogWarning("Ran out of items, ending current block and skipping subsequent blocks");
                    log.log("WARNING - Ran out of items, ending current block and skipping subsequent blocks", 1);
                    parentTask.skip = true;
                    break;
            }
        }
    }
}


