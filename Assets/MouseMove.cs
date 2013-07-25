using UnityEngine;
using System.Collections;

public class MouseMove : MonoBehaviour {
Vector3 mousePos;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () 
	{
	mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
	if (Input.GetMouseButton(0))
			{						
			Vector3 movePos = new Vector3(mousePos.x, mousePos.y, 90);	
			transform.position = movePos;				
			}	
	}
}
