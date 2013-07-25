using UnityEngine;
using System.Collections;
using System.Collections.Generic;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]

public class RaycastShadowsOrig : MonoBehaviour {
public GameObject meshHolder;
public bool takeVectorPoint;
public bool makeAVectorList;
public float numberOfRays;
int vectorPointNumber = 0;
public float angle;
public List<Vector3> vectorList;
public List<Vector3> vectorList2;
Vector3 newVertices;	
	
public Vector3 prevVector;
//public Mesh mesh;
public Vector3 _playerPos;
RaycastHit hit;

	void Start () {
		
	makeAVectorList = true;	
	DetectObjects();
	_playerPos = transform.position;
//	vectorList.Add(hit.point);
//	vectorList2.Add(hit.point);
	}
	

	void Update ()
	{	
	DetectObjects();
	_playerPos = new Vector3(transform.position.x,transform.position.y,-20) ;
	}	
	
	void DetectObjects()
		
	{	
		vectorList.Clear();
		angle = 0;
		for (float i=1; i<numberOfRays; i++)
			{
			vectorPointNumber = 0;
//			RaycastHit hit;
			angle += 2*Mathf.PI/numberOfRays;
			Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);			
				if (Physics.Raycast(transform.position, direction, out hit))
			      	{
//					Debug.Log(hit.point);
			        Debug.DrawLine(transform.position, hit.point, Color.green);
					takeVectorPoint = true;
					prevVector = hit.point;
					}
				if (!Physics.Raycast(transform.position, direction, out hit))
					{
//					Debug.DrawLine(transform.position, direction*200, Color.red);
					if (takeVectorPoint == true && makeAVectorList == true)
						{
//						Debug.Log("Hello1");
//						vectorListpt2.Add(prevVector);						
//						vectorListpt2[vectorPointNumber] = prevVector;						
//						vectorList.Add(hit.point);
//						vectorList[vectorPointNumber] = new Vector3(hit.point.x, hit.point.y, 0);
						vectorList.Insert(vectorPointNumber, new Vector3(hit.point.x, hit.point.y, 0));
						vectorPointNumber ++;
						takeVectorPoint = false;
						}
					}			
			}		
		for (float i=1; i>-numberOfRays; i--)
			{
//			vectorPointNumber = 0;
//			RaycastHit hit;
			angle -= 2*Mathf.PI/numberOfRays;
			Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
				if (Physics.Raycast(transform.position, direction, out hit))
			      	{
//					Debug.Log(hit.point);
//			        Debug.DrawLine(transform.position, hit.point, Color.green);
					takeVectorPoint = true;
//					prevVector = hit.point;
					}
				if (!Physics.Raycast(transform.position, direction, out hit))
					{
//					Debug.DrawLine(transform.position, direction*200, Color.red);
					if (takeVectorPoint == true && makeAVectorList == true)
						{
//						Debug.Log("Hello2");
//						vectorListpt2.Add(prevVector);						
//						vectorListpt2[vectorPointNumber] = prevVector;						
//						vectorList2.Add(hit.point);
						vectorList.Insert(vectorPointNumber, new Vector3(hit.point.x, hit.point.y, 0));
						vectorPointNumber ++;
						takeVectorPoint = false;
						}
					}			
			}
	DrawMesh();	
	}	
	
	
	void DrawMesh()
	{
//		for (int i = 0; i<7; i++)	
//		{
		Mesh mesh = meshHolder.GetComponent<MeshFilter>().mesh;
		mesh.Clear();
//			mesh.vertices = new Vector3[] 
//			{
//			_playerPos,
//			vectorList[i],
//			vectorList2[i],
//			};			
//			mesh.uv = new Vector2[] 
//			{
//			_playerPos,
//			vectorList[i],
//			vectorList2[i],
//			};				
//			mesh.normals = new Vector3[] 
//			{
//			_playerPos,
//			vectorList[i],
//			vectorList2[i],
//			};					
//			mesh.triangles = new int[] {0,1,2};			
//		}
//			vectorList.Clear();
//			vectorList2.Clear();
//			mesh.Clear();
		
		
		Vector3[] vertices = mesh.vertices;
		Vector2[] uv = mesh.uv;
		vertices = new Vector3 [vectorList.Count+1];
		uv = new Vector2 [vectorList.Count+1];
		int[] triangles = mesh.triangles;
		triangles = new int [vectorList.Count*3];
		
		for(int v = 1, t = 1; v < vectorList.Count; v++, t += 3)
		{
			vertices[v] = vectorList[v];
			triangles[t] = v;
			triangles[t + 1] = v + 1;
		}
		triangles[triangles.Length - 1] = 1;
		
//			int i = 0;
//	       	while (i < vectorList.Count) 
//				{		      
//				vertices[i] = vectorList[i];
//				uv[i] = vectorList[i];
////				Debug.Log(vertices);
//	        	i++;
//	       		}
		mesh.vertices = vertices;
		mesh.uv = uv;
		mesh.triangles = triangles;
//		mesh.Clear();
//		vectorList.Clear();
		
//		for (int n= 0; n<vectorList.Count; n++)
//			{
//			newVertices = vectorList[n];			
//			}
//		Debug.Log(newVertices);
			
//			   int i = 0;
//        while (i < vertices.Length) {
//            vertices[i] += normals[i] * Mathf.Sin(Time.time);
//            i++;
//        }
//        mesh.vertices = vertices;
		
//		
//		Vector2[] uvs = new Vector2[vectorList.Count+1];
//    	Vector3[] newvertices = new Vector3[vectorList.Count+1];
//    	for (int n = 0; n<newvertices.Length-1;n++) 
//				{
//				newvertices[n] = new Vector3(vectorList[n].x,vectorList[n].y,vectorList[n].z );
//				}
//		int[] triangles = new int[newvertices.Length*3];
//
//	// create some uv's for the mesh?
//	// uvs[n] = vertices2d[n];
//		
////    	}
//		
//	int	i = -1;
//	for (int n=0;n<triangles.Length-3;n+=3)
//	{
//		i++;
//		triangles[n] = vectorList.Count-1;
//		if (i>=vectorList.Count)
//		{
//			triangles[n+1] = 0;
//			//print ("hit:"+i);
//		}else{
//			triangles[n+1] = i+1;
//		}
//		triangles[n+2] = i;
//	}    
//    i++;
//	// central point
//	newvertices[newvertices.Length-1] = new Vector3(0,0,0);
//	triangles[triangles.Length-3] = vectorList.Count-1;
//	triangles[triangles.Length-2] = 0;
//	triangles[triangles.Length-1] = i-1;
//   
//    // Create the mesh
//    //var msh : Mesh = new Mesh();
//    mesh.vertices = newvertices;
//    mesh.triangles = triangles;
//    mesh.uv = uvs;
//		
//		
		
		
	}
	
	
	IEnumerator WaitForTime()
		{
//		Debug.Log ("hello3");
		yield return new WaitForSeconds(0.1f);		
		}
}
