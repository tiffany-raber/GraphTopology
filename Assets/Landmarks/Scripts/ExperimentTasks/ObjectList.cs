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
using System;
using System.Collections.Generic;

public class ObjectList : ExperimentTask {

    [Header("Task-specific Properties")]

    public string parentName = "";
	public GameObject parentObject;
	public int current = 0;
	[Tooltip("Use experiment run to index where to start in this list")]
	public bool configControlCurrent;
	public TaskList taskListControlCurrent;
	
	public List<GameObject> objects;
	public EndListMode EndListBehavior; 
	public bool shuffle;
	public List<ObjectList> ignoreCurrentFromOtherLists = new List<ObjectList>();
	private bool first = true;
    // public GameObject order; // DEPRICATED

    
	public override void startTask () {
        //ViewObject.startObjects.current = 0;
        //current = 0;
        
		// DEPRECATED
		// if (order ) {
		// 	// Deal with specific ordering
		// 	ObjectOrder ordered = order.GetComponent("ObjectOrder") as ObjectOrder;
		
		// 	if (ordered) {
		// 		Debug.Log("ordered");
		// 		Debug.Log(ordered.order.Count);
				
		// 		if (ordered.order.Count > 0) {
		// 			objs = ordered.order.ToArray();
		// 		}
		// 	}
		// }
		
		TASK_START();
	 
	}	
	
	public override void TASK_ADD(GameObject go, string txt) {
		objects.Add(go);
	}
	
	public override void TASK_START()
	{
		base.startTask();		
		if (!manager) Start();

		if (first && configControlCurrent && taskListControlCurrent != null && manager.config.run > 1)
		{
			current = (manager.config.run - 1) * taskListControlCurrent.repeat + 1;
		} 

		if (first) first = false;

        GameObject[] objs;

        if (objects.Count == 0)
        {
            if (parentObject == null & parentName == "") Debug.LogError("No objects found for objectlist.");

            // If parentObject is left blank and parentName is not, use parentName to get parentObject
            if (parentObject == null && parentName != "")
            {
                parentObject = GameObject.Find(parentName);
            }

            objs = new GameObject[parentObject.transform.childCount];

            Array.Sort(objs);

            for (int i = 0; i < parentObject.transform.childCount; i++)
            {
                objs[i] = parentObject.transform.GetChild(i).gameObject;
            }
        }
        else
        {
            objs = new GameObject[objects.Count];
            for (int i = 0; i < objects.Count; i++)
            {
                objs[i] = objects[i];
            }
        }

        if (shuffle)
        {
            Experiment.Shuffle(objs);
        }


		// Initialize and populate the objects in our ObjectList
        objects = new List<GameObject>();

        foreach (GameObject obj in objs)
        {
			// Check any other ObjectLists provided and don't add the current object from those lists
			if (ignoreCurrentFromOtherLists.Count > 0)
			{
				foreach (var ol in ignoreCurrentFromOtherLists)
				{
					if (ol.currentObject() == obj) continue;
					else objects.Add(obj);
				}
			}
			else objects.Add(obj);

			foreach (var o in objects) log.log("TASK_ADD\t" + name + "\t" + this.GetType().Name + "\t" + o.name + "\t" + "null", 1);
			
        }
    }
	
	public override bool updateTask () {
	    return true;
	}
	public override void endTask() {
		//current = 0;
		TASK_END();
	}
	
	public override void TASK_END() {
		base.endTask();

		// Not sure this code is functioning correctly (and may be causing issues)
		//if (canIncrementLists)
		//{
		//	foreach (var ol in ignoreCurrentFromOtherLists)
		//	{
		//		ol.incrementCurrent();
		//	}
		//}
    }
	
	public GameObject currentObject() {
		if (current >= objects.Count) {
			return null;
		} else {
			return objects[current];
		}
	}
	
	public new void incrementCurrent() 
	{
		current++;
		
		if (current >= objects.Count && EndListBehavior == EndListMode.Loop) {
			current = 0;
		}
	}
}
