using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//Script for sword rotation
public class Rotation : MonoBehaviour {
    public float speed = 1;
    public float speed2 = 0.5f;
    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        transform.rotation *= Quaternion.Euler(0, speed, 0) * Quaternion.Euler(speed2,0,0);
	}
}
