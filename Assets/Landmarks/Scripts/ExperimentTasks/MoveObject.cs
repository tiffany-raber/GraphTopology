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

public class MoveObject : ExperimentTask {

	[Header("Task-specific Properties")]
	public GameObject start;
	public GameObject destination;
	public ObjectList destinations;
	public bool useLocalRotation = true;
	
	public bool swap;
	public bool useDestinationSnapPoint;
	[Tooltip("Specify an offset from the destination to move the object to and have it face the destination object")]
	public Vector3 localOffsetFacing;

	private static Vector3 position;
	private static Quaternion rotation;


	public override void startTask () {
		TASK_START();
	}	

	public override void TASK_START()
	{
		base.startTask();
		
		if (!manager) Start();
		
		
		if (skip) {
			log.log("INFO	skip task	" + name,1 );
			return;
		}
		
		if ( destinations ) {
			destination = destinations.currentObject();		
		}

		// Temporary variable for the destination in case we are using a child's transform for movement
		GameObject destinationLocationObject;
		if (useDestinationSnapPoint) destinationLocationObject = destination.GetComponentInChildren<LM_SnapPoint>().gameObject;
		else destinationLocationObject = destination;

		// Lock in the start location
		position = start.transform.position;
		if (useLocalRotation) rotation = start.transform.localRotation;
        else rotation = start.transform.rotation;

       
		start.transform.position = destinationLocationObject.transform.position;
		

		if (useLocalRotation) start.transform.localRotation = destinationLocationObject.transform.localRotation;
		else start.transform.rotation = destinationLocationObject.transform.rotation;

		// If using an offset and having the start face the destination object
		if (localOffsetFacing != Vector3.zero)
		{
			start.transform.position = destinationLocationObject.transform.position + destinationLocationObject.transform.TransformDirection(localOffsetFacing);

			start.transform.LookAt(destinationLocationObject.transform);
		}

		log.log("TASK_ROTATE\t" + start.name + "\t" + this.GetType().Name + "\t" + start.transform.localEulerAngles.ToString("f1"), 1);
		log.log("TASK_POSITION\t" + start.name + "\t" + this.GetType().Name + "\t" + start.transform.transform.position.ToString("f1"),1);
		
		if (swap) {
			destination.transform.position = position;
			if (useLocalRotation) destination.transform.localRotation = rotation;
			else destination.transform.rotation = rotation;
		}
	}
	
	public override bool updateTask () {
	    return true;
	}
	public override void endTask() {
		TASK_END();
	}
	
	public override void TASK_END() {
		base.endTask();
		
		if ( destinations ) {
			if (canIncrementLists)
			{
				destinations.incrementCurrent();
				destination = destinations.currentObject();
			}
		}
	}
}
