using UnityEngine;
using System.Collections;
using System.Collections.Generic;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]

public class FindFaces : MonoBehaviour {
public bool segSwitch;
public float enlargeScale;
Vector3 _enlargeScale;
public GameObject meshHolder;
public GameObject marker;
GameObject[] polygon;
public int BoundaryPoints;
public float Shadowlength;
GameObject[] walls;	
public float[] testArray;
int polyNumber;
//bool lastHit;
//bool meshDrawn;
//public int numberOfRays;
//int vectorPointNumber = 0;
int _verticesArraySize;
public int verticesArraySize;
//float angle;
//public List<Vector3> vectorList;
//public List<Vector3> vectorList2;
//Vector3 newVertices;	
//Vector3 prevVector;
//Vector3 _playerPos;
//Vector3 tmp;
//Vector3 tmp2;
//RaycastHit hit;
//Mesh mesh;
public Vector3[] vertices;
Vector3[] verticesTemp;
public Vector3[] normals;
public int[] triangles;
public List <Vector3> polyTransform;
public List <GameObject> polygonNumber;
public List <Vector3> verticesList;
public List <Vector3> _verticesList;
public List <Vector3> verticesAngles;
public List <Vector3> verticesByNormals;
public List <Vector3> vectorListSeg;
Vector3 verticesDirection;
Vector3 vertNudge;
float vertexAngle;
Vector3 _verticesListTemp;
public List <Vector3> verticesListTemp;
public List <Vector3> segList;
public Vector3[] segArray;
public bool[] segPoint;
float verticesAngle;
public Vector3[] finalVertices;	
float[] actualAngle;
public float[] _actualAngle;
float tempval;
bool getPolys;
public bool meshBuilt;
RaycastHit hit;

Vector3 movePos;
Vector3 rayPos;
	
	void Start () 
	{				
		walls = GameObject.FindGameObjectsWithTag("Walls");		// initiate array to include all polygons tagged 'wall'	
//		ScanForObjects();
//		DetectObjectVertices();
//		AddBoundaryPoints(); 											// add the perimeter points	
//		CompareVerticesAngles(verticesList,transform.position);			// arrange global vertices list into CW order
//		verticesList.Clear ();
//		for (int iP = 0; iP<verticesListTemp.Count; iP++) // add newly arranged CW vertices to list
//			{
//			verticesList.Add (verticesListTemp[iP]);			
//			}	
//		DrawMesh();
		
	}	

	void Update ()
		{	
			//assign all shadow casting objects in scene to wall array			
			walls = GameObject.FindGameObjectsWithTag("Walls");					// initiate array to include all polygons tagged 'wall'	
			ScanForObjects();
			DetectObjectVertices();												// detect all poly's vertices
			AddBoundaryPoints(); 												// add the perimeter points	
			CompareVerticesAngles(verticesList, transform.position);			// arrange global vertices list into CW order
			verticesList.Clear ();
				for (int iP = 0; iP<verticesListTemp.Count; iP++) 				// add newly arranged CW vertices to list
					{
					verticesList.Add (verticesListTemp[iP]);			
					}
			DrawMesh();
			}
	
	#region Analyse the scene for objects to use as walls
	
	public void ScanForObjects()						// finds objects in scene, label them
	{					
		polygon = new GameObject[walls.Length]; 		
		polyNumber = 0;
		foreach(GameObject wall in walls)
			{
			polygon[polyNumber] = wall.gameObject;
			polyNumber++;
			}
	}	
	#endregion
	
	#region Finding the vertices for applicable polygons
	
	public void DetectObjectVertices()					// establishes vertices of all shadow casting objects in scene, check to see which vertices are visible by player		
	{			
		// number of useful vertices in scene
		verticesArraySize = 0;
		// cycle through walls, selecting vertices					
		for (int _polyNumber = 0; _polyNumber<walls.Length; _polyNumber++)
			{
			polyNumber = _polyNumber;
//			polygon[polyNumber].renderer.enabled = true;
			// highlight polygon being examined in red
//			polygon[_polyNumber].renderer.material.color = Color.red;						
			// get mesh of current polygon
			Mesh meshHit = polygon[_polyNumber].GetComponent<MeshFilter>().mesh;	
			// determine size of vertices array by defining how many vertices are facing down (i.e. the bottom of the polygon) using normals
			normals = meshHit.normals;
			verticesTemp = meshHit.vertices;						
			for (int i=0; i<meshHit.normals.Length ; i++)
				{
				if (normals[i].z == 1)
					{
					verticesByNormals.Add(verticesTemp[i]);					
					}		
				}									
			verticesTemp = verticesByNormals.ToArray();		
			vertices = new Vector3[verticesByNormals.Count+1];			// assign size of vertices array, +1 to include player position
			for (int i=1; i<verticesByNormals.Count+1; i++)
				{							
					// adjust vertices for scale of poly
					vertices[i] = new Vector3 (verticesTemp[i-1].x*polygon[_polyNumber].transform.lossyScale.x, verticesTemp[i-1].y*polygon[_polyNumber].transform.lossyScale.y,transform.position.z);
					vertices[i] = (polygon[_polyNumber].transform.position - vertices[i]); // adjust for world position
					vertices[i].z = transform.position.z;	
					
					Vector3 BoundTest = vertices[i]+ (vertices[i]-transform.position)*-0.001f ; // create a test vertex, move it towards player to avoid problems with linecast detection
					if (!polygon[_polyNumber].collider.bounds.Contains(BoundTest)) // if test vertex isn't within bounds of poly, it's visible
						{
						RaycastHit hit;
						if (!Physics.Linecast (BoundTest, transform.position, out hit))
							{
							_verticesList.Add (vertices[i]);
							}
						}
				}						

			CompareVerticesAngles(_verticesList, transform.position);		// arrange single polygon's vertices into CW order					
			AddVerticesToList(); 						// add to global vertices list
			_verticesList.Clear(); 					
			verticesByNormals.Clear();
			}				
		}
	
	#endregion
		
	#region Adding extra vertices	
	
	public void AddBoundaryPoints()							// add perimeter points to complete mesh if visible
	{
		Vector3[] boundaryPoint = new Vector3[BoundaryPoints];
		for (int iBound = 0; iBound<BoundaryPoints; iBound++)
			{
			float boundAngle = 360f * ((iBound+1f)/BoundaryPoints);				// took a long time to figure out the 1 needed an f after it...			
			float boundVectorX = transform.position.x + Screen.width * Mathf.Cos(boundAngle * Mathf.PI / 180);
			float boundVectorY = transform.position.y + Screen.width * Mathf.Sin(boundAngle * Mathf.PI / 180);				
			boundaryPoint[iBound] = new Vector3(boundVectorX, boundVectorY, transform.position.z);
			}		
		
		RaycastHit hit;	
		for (int iBP = 0; iBP< BoundaryPoints; iBP++)
			{
			if (!Physics.Linecast (transform.position, boundaryPoint[iBP], out hit)) 
					{
					verticesList.Add (boundaryPoint[iBP]);
					Debug.DrawLine (boundaryPoint[iBP],transform.position,Color.blue);											
					}			
			}
	}	
	
	
	public void AddVerticesToList() 					// adding vertices of poly to global vertices list 
	{
		
//	if (verticesList.Count == 0 && verticesListTemp[0].x>transform.position.x && verticesListTemp[verticesListTemp.Count-1].x<transform.position.x && verticesListTemp[0].y>transform.position.y )
//		{
//		Debug.Log (""+transform.position.x+" "+verticesListTemp[0].x+" "+verticesListTemp[verticesListTemp.Count-1].x+" ");		
//		Vector3 tempVertices = verticesListTemp[0];
//		verticesListTemp[0] = verticesListTemp[verticesListTemp.Count-1];
//		verticesListTemp[verticesListTemp.Count-1] = tempVertices;		
//		}
//	else
//		{		
		if (verticesListTemp.Count==1) // if poly only has one vertex visible, find out which edge of poly it's on, so that final CompareVerticesAngles sweep puts it in correct order
			{
			MovePointOnCircle(-0.001f, vertexAngle, verticesListTemp[0], 0);
			Vector3 _checkBoundsLeft = vertNudge;
			MovePointOnCircle(0.001f, vertexAngle, verticesListTemp[0], 0);
			Vector3 _checkBoundsRight = vertNudge;
			
			// create imaginary point based on vertex and see whether, if you move it left or right, it is then in poly's boundary, thereby judging which side of poly it is on
			
			if (!polygon[polyNumber].collider.bounds.Contains(_checkBoundsLeft))	
				{				
				ExtrapolateLastVector();					
				}				
			if (!polygon[polyNumber].collider.bounds.Contains(_checkBoundsRight))
				{
				ExtrapolateFirstVector();
				}
			if (!polygon[polyNumber].collider.bounds.Contains(_checkBoundsLeft) && !polygon[polyNumber].collider.bounds.Contains(_checkBoundsRight)) 
				{ // if vertex is frontmost point on poly, _checkbounds has to be moved away from player a smidge, so as to be inside the poly when being moved left or right
				MovePointOnCircle(-0.001f, vertexAngle, verticesListTemp[0], 0.001f);
				_checkBoundsLeft = vertNudge;
				MovePointOnCircle(0.001f, vertexAngle, verticesListTemp[0], 0.001f);
				_checkBoundsRight = vertNudge;
				if (!polygon[polyNumber].collider.bounds.Contains(_checkBoundsLeft))		
					{				
					ExtrapolateLastVector();					
					}				
				if (!polygon[polyNumber].collider.bounds.Contains(_checkBoundsRight))
					{
					ExtrapolateFirstVector();
					}
				}			
			}
		
		if (verticesListTemp.Count>1)	// if poly has more than one vertex visible, use vertices on opposing edges to calculate shadow
			{				
			ExtrapolateFirstVector();				
			for (int iVert = 1; iVert< verticesListTemp.Count-1; iVert++) // add middle vertices of poly
				{
				verticesList.Add (verticesListTemp[iVert]);	
				}	
			ExtrapolateLastVector();
			}		
		
//		if (verticesListTemp.Count==0)
//			{
//			polygon[polyNumber].renderer.enabled = false;	
//			}
//		}
		verticesListTemp.Clear();
		verticesAngles.Clear ();
		
	}
	
	
	public void ExtrapolateFirstVector()		// creating, clockwise speaking, first edge of shadow for a poly by extruding edge vertex to either poly behind it or arbitrary distance off screen
		{
			FindVertexAngle(verticesListTemp[0],transform.position); 	// find angle of start vertex of poly (CW speaking)		
				
			MovePointOnCircle(0, vertexAngle, verticesListTemp[0], -0.1f);			
			Vector3 _checkBounds = vertNudge;
			MovePointOnCircle(-0.001f, vertexAngle, verticesListTemp[0], 0);
			_verticesListTemp = vertNudge;
			if (!polygon[polyNumber].collider.bounds.Contains(_checkBounds))
				{
				if (Physics.Raycast(verticesListTemp[0], (verticesListTemp[0]- transform.position), out hit)) 	// raycasting outwards from first CW poly point, if it hits another poly, use that point...
					{
		            verticesListTemp[0] = hit.point;		
					Debug.DrawLine (_checkBounds,verticesListTemp[0],Color.yellow);
					}		
				else
					{
					verticesListTemp[0] = verticesListTemp[0]+((verticesListTemp[0]- transform.position)*Shadowlength); // if not, extrude point an arbitrary distance (Shadowlength)		
					Debug.DrawLine (_verticesListTemp,verticesListTemp[0],Color.cyan);
					}		
				verticesList.Add(verticesListTemp[0]);			// add extruded poly vertex (shadow edge point) to global vertex list		
				verticesList.Add(_verticesListTemp);			// add original vertex of poly to global vertex list		
				}		
		}
	
	public void ExtrapolateLastVector()		// creating, clockwise speaking, last edge of shadow for a poly
		{
			FindVertexAngle(verticesListTemp[verticesListTemp.Count-1],transform.position);			
			MovePointOnCircle(0, vertexAngle, verticesListTemp[verticesListTemp.Count-1], -0.1f);
			Vector3 _checkBounds = vertNudge;
			MovePointOnCircle(0.001f, vertexAngle, verticesListTemp[verticesListTemp.Count-1], 0);
			_verticesListTemp = vertNudge;
				
			if (!polygon[polyNumber].collider.bounds.Contains(_checkBounds))
				{						
				if (Physics.Raycast(verticesListTemp[verticesListTemp.Count-1], (verticesListTemp[verticesListTemp.Count-1]- transform.position), out hit))
					{
		            verticesListTemp[verticesListTemp.Count-1] = hit.point;				
					Debug.DrawLine (_checkBounds,verticesListTemp[verticesListTemp.Count-1],Color.green);
					}
				else
					{			
					verticesListTemp[verticesListTemp.Count-1] = verticesListTemp[verticesListTemp.Count-1]+((verticesListTemp[verticesListTemp.Count-1]- transform.position)*Shadowlength); 			
					Debug.DrawLine (_checkBounds,verticesListTemp[verticesListTemp.Count-1],Color.red);
					}			
				verticesList.Add(_verticesListTemp);							// add original vertex of poly to global vertex list
				verticesList.Add(verticesListTemp[verticesListTemp.Count-1]);	// add extruded poly vertex to global vertex list
				}
		}
	
	#endregion
		
	#region Analysing the vertices
	
	public void CompareVerticesAngles(List<Vector3> verticesAngles, Vector3 centrePoint)			//establish vertices angle in relation to player, sort in clockwise order
	{
		// -part 1: assign angles to list
		actualAngle = new float[verticesAngles.Count];	
		for (int i=0; i<verticesAngles.Count; i++)
			{
			FindVertexAngle(verticesAngles[i], centrePoint);
			actualAngle[i] = vertexAngle;				
			}
		testArray = new float[verticesAngles.Count]; 
		for (int i=0; i<actualAngle.Length; i++)
			{
//			actualAngle[i] = Mathf.Round(actualAngle[i]*100)/100; // rounding angles to 2 decimal places
			testArray[i] = actualAngle[i];				
			}
		
		// -part 2: compare to other angles
		float sortCheckSize = actualAngle.Length;			//number of angles to check through
		int iV = 0;	
		while (sortCheckSize > 0)
			{	
			while (actualAngle[iV] == 361)					// cycle through vertices that have already been scanned
				{
				iV++;
				}
			if (actualAngle[iV] != 361)						// if vertex has not been scanned
				{
				tempval = actualAngle[iV];					// use temporary value for angle to be compared
				}
			 for (int iN=0; iN<actualAngle.Length;iN++)
				{					
				if ((tempval<= actualAngle[iN]))			// if angle to be compared is lower or equal to the comparison angle...
					{
						if (iN == actualAngle.Length-1)		// .. and there's no more angles to compare it to...
						{					
						if (getPolys== false)				//// if function is being used for comparing angle of individual vertices
							{							
							verticesListTemp.Add (verticesAngles[iV]); 			// if angle is smallest, add correspondingly numbered vertex to global list
							}
						else 								// when function is being used for comparing angle of entire polygons
							{
							polygonNumber.Add (polygon[iV]);
							}
						sortCheckSize -= 1;
						actualAngle[iV] = 361;
						iV = 0;
						}					
					}
				if (tempval> actualAngle[iN])				// if angle to be compared is larger than comparison angle, exit loop, moving onto next angle
					{
					iV++;
					iN = actualAngle.Length-1;				
					}
				}			
	        }
			
	}
	
	public void FindVertexAngle (Vector3 vertexCoord, Vector3 centrePoint)				// used to find angle of single vertex
		{		
		vertexAngle = 0;
		verticesDirection = vertexCoord - centrePoint;
		float _angle = Vector3.Angle(verticesDirection,Vector3.up);		
		float dirNum = AngleDir(Vector3.up, verticesDirection, Vector3.forward); 		
			if(dirNum>0F)
				{
				vertexAngle += 360F-_angle;				
				}
				else
				{
				vertexAngle += _angle;
				}
		}	
	
		private float AngleDir(Vector3 fwd, Vector3 targetDir, Vector3 up) 	//variable to turn acute 180 degree angles into 360 degree angles
			{ 
			Vector3 perp = Vector3.Cross(fwd, targetDir);
			float dir = Vector3.Dot(perp, up);		
			if (dir > 0f) {
				return 1f;
			} else if (dir < 0f) {
				return -1f;
			} else {
				return 0f;
			}
			
		}	
	
	public void MovePointOnCircle (float addAngle, float vertexAngle, Vector3 _vertNudge, float moveTowardPlayer) // used to shift vertices around player in a circle
	
		{
		vertexAngle = 360 - vertexAngle + 90 + addAngle;			
		float vertexDist = (Vector3.Distance(transform.position,_vertNudge)) - moveTowardPlayer; 
		float addShadX = transform.position.x + vertexDist * Mathf.Cos(vertexAngle * Mathf.PI / 180); 
		float addShadY = transform.position.y + vertexDist * Mathf.Sin(vertexAngle * Mathf.PI / 180);
		vertNudge = new Vector3(addShadX, addShadY, _vertNudge.z);}			
	
	
	public void CreateSegments (List<Vector3> _vectorListSeg) 				// used to analyse each segment (point between vertices)
			
		{
//		segList.Clear();
		
		segArray = new Vector3[_vectorListSeg.Count]; 
		segPoint = new bool [_vectorListSeg.Count];		
		vectorListSeg.Clear ();
		int iSeg = 0;
		RaycastHit hit; 
		for (iSeg = 0; iSeg<_vectorListSeg.Count; iSeg++)
			{		
			if (iSeg+1 <= _vectorListSeg.Count-1)
				{
				segArray[iSeg] = ((_vectorListSeg[iSeg] + _vectorListSeg[iSeg+1])/2);										// find midpoint of segment
				if (!Physics.Linecast(transform.position, segArray[iSeg], out hit)) 										// check to see if midpoint of 1st segment is visible
					{				
					segPoint[iSeg] = true;
					Debug.DrawLine (_vectorListSeg[iSeg],_vectorListSeg[iSeg+1],Color.magenta);
					}
				else
					{
					segPoint[iSeg] = false;	
					}			
				}
			if (iSeg+1 > _vectorListSeg.Count-1)
				{
				segArray[_vectorListSeg.Count-1] = ((_vectorListSeg[_vectorListSeg.Count-1] + _vectorListSeg[0])/2);		// analysing segment between last and first vertices
				if (!Physics.Linecast(transform.position, segArray[_vectorListSeg.Count-1], out hit)) 						// check to see if midpoint of segment is visible
					{	
					segPoint[_vectorListSeg.Count-1] = true;
					Debug.DrawLine (_vectorListSeg[_vectorListSeg.Count-1],_vectorListSeg[0],Color.magenta);
					}
				else
					{
					segPoint[_vectorListSeg.Count-1] = false;	
					}
				}
			}
		
		
		for (iSeg = 0; iSeg< _vectorListSeg.Count; iSeg++)
			{
//		
			if (iSeg+1 <= _vectorListSeg.Count-1)
				{				
				if (segPoint[iSeg] == false)				
					{
					if (segPoint[iSeg+1] == false)
						{	
						}
					else {vectorListSeg.Add(_vectorListSeg[iSeg+1]);}
					}
				else {vectorListSeg.Add(_vectorListSeg[iSeg+1]);}
				}
			
			if (iSeg+1 > _vectorListSeg.Count-1)
				{					
				if (segPoint[_vectorListSeg.Count-1] == false)				
					{
					if (segPoint[0] == false)
						{	
						}
					else {vectorListSeg.Add(_vectorListSeg[0]);}
					}
				else {vectorListSeg.Add(_vectorListSeg[0]);}	
				}
//			vectorListSeg.Add(_vectorListSeg[iSeg]);
			
			}
//		if (segPoint[_vectorListSeg.Count-1] == true && segPoint[0] == true)
//				{
//				vectorListSeg.Add(_vectorListSeg[0]);
//				}
		
//				vectorListSeg.Add(_vectorListSeg[iSeg]);
//				Debug.DrawLine (_vectorListSeg[iSeg],_vectorListSeg[iSeg+1],Color.magenta);
		
		}
	
	
	#endregion
		
	#region Drawing the mesh	
	
	void DrawMesh()		// create mesh for main lightmesh
		{	
		Mesh mesh = meshHolder.GetComponent<MeshFilter>().mesh;
		finalVertices = new Vector3 [verticesList.Count+1];
		Vector2[] finalUV = new Vector2 [verticesList.Count+1];
		// assign triangles	
		triangles = new int [verticesList.Count*3];	
		mesh.triangles = triangles;
		for(int v = 1, t = 1; v < verticesList.Count+1; v++, t += 3)
			{				
				finalVertices[v] = verticesList[v-1];
				// make sure lightmesh is flat
				Vector3 tempvert = finalVertices[v-1];
				tempvert.z = transform.position.z;
				finalVertices[v-1] = tempvert;
				finalUV[v-1] = new Vector2(tempvert.x, tempvert.y);
				triangles[t] = v;
				triangles[t + 1] = v + 1;
			}
		triangles[triangles.Length-1] = 1;	
		finalVertices[0] = transform.position;
//		mesh.triangles = triangles;	
		mesh.vertices = finalVertices;
		mesh.uv = finalUV;
		mesh.triangles = triangles;		
		verticesList.Clear ();
		verticesListTemp.Clear();
		meshBuilt = true;
		
		}
	
	#endregion
}