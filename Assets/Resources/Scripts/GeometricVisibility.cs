using UnityEngine;
using System.Collections;
//List Usage
using System.Collections.Generic;

/*	GeometricVisibility
	
Steps:
	1.	get all polygons
	1.1.	
	1.2.	

*/

public class GeometricVisibility : MonoBehaviour {
	
	#region VARIABLES
	
	private	Camera cam;			//camera, for panning zooming etc.
	public	Transform source;	//the Transform of the lightSource, for position setting/getting
	
	//shadow caster GameObjects
	GameObject[] walls;
	
	//polygons as struct allowing to have objects references
	private BottomPolygon[]	staticBlockers; //immoveable walls
	//private BottomPolygon[]	dynamicBlockers; //dynamic blockers, recalc positions for relevance
		
	private BottomPolygon[]	randomPolys; //immoveable walls
	
	private struct BottomPolygon{
		//defining ordering: CCW viewed from top
		
		//vertices of this polygon, Vector2 would be ok too
		public Vector3[] vertices;
								
		//the center position
		public Vector3 position;
		
		//radius around center where this polygon has to be taken into calculation
		public float relevantRadius;
		
		//the corresponding fader of the wall object that fades out currently invisible walls... future
		public VisibilityFader fader;
		
		//corresponding Transform to recalc vertices in WorldCoordinates if needed
		public Transform transform;
	}
	
	#endregion
	
	
	
	#region STARTUP & UPDATE
	
	private	void Awake(){
		cam = transform.camera;
	}
	
	private	void Start(){
		walls = GameObject.FindGameObjectsWithTag("Wall");
		
		//Struct
		GeneratePolygonStructArr(ref staticBlockers);
		
		//init randPolys
		ClearRandomPolygons(ref randomPolys);
	}
	
	private	void Update () {		
		CameraManipulation();
		polyCount = randomPolys.Length;
				
		//Struct				//extracted polygons						//generated polygons without gameobject
		if(drawLineToVertices)	{ DrawLineToVertices(ref staticBlockers);	DrawLineToVertices(ref randomPolys);}
		if(drawPolygons)		{ DrawPolygons(ref staticBlockers);			DrawPolygons(ref randomPolys);}
		if(drawVisibleFaces)	{ DrawVisibleFaces(ref staticBlockers);		DrawVisibleFaces(ref randomPolys);}
		if(drawSegments)		{ }
		if(drawLines)			{ }
		if(drawCuts)			{ }
		if(drawRecalc)			{ }
		if(drawFinal)			{ }
				
		if(fadeTest)			{FaderTest(ref staticBlockers);}
		if(autoGen)				{CreateRandomPolygon(ref randomPolys);}
	}
	#endregion
	
	#region DRAWLINE
	
	//just to see the order of the vertices, different colors to see order
	private	Color[] colors = new Color[] {new Color(0F,0F,1F,1F), new Color(0F,0F,1F,0.6F), new Color(0F,0F,1F,0.4F), new Color(0F,0F,1F,0.2F), };
	private	void DrawLineToVertices(ref BottomPolygon[] poly){
		for(int ip = 0; ip < poly.Length; ip++){	//polygon
			for(int iv = 0; iv < poly[ip].vertices.Length; iv++){	//vertices of the polygon
				Color color = new Color(0F,0.5F,1F,1F);
				if(iv != 0){color = colors[iv%4];}
				Debug.DrawLine(source.position, poly[ip].vertices[iv], color, 0F, false);
			}
		}
	}
	
	//draw the polygon CYAN
	private	void DrawPolygons(ref BottomPolygon[] poly){
		for(int ip = 0; ip < poly.Length; ip++){	//polygon
			for(int iv = 0; iv < poly[ip].vertices.Length; iv++){	//vertices of the polygon
				int nv = (iv+1)%poly[ip].vertices.Length; //next vertex
				Debug.DrawLine(poly[ip].vertices[iv], poly[ip].vertices[nv], new Color(0F,1F,1F,0.4F), 0F, false);
			}
		}
	}
	
	private	void DrawVisibleFaces(ref BottomPolygon[] poly){
		for(int ip = 0; ip < poly.Length; ip++){	//polygon
			for(int iv = 0; iv < poly[ip].vertices.Length; iv++){	//vertices of the polygon
				int nv = (iv+1)%poly[ip].vertices.Length; //next vertex
				
				Vector3 vertexDirection = poly[ip].vertices[iv]-poly[ip].vertices[nv];
				Vector3 sourceDirection = source.position-poly[ip].vertices[iv];
				if(!invisibleFaces){
					if( AngleDir(vertexDirection,sourceDirection,Vector3.up)<0F ){
						Debug.DrawLine(poly[ip].vertices[iv], poly[ip].vertices[nv], Color.white,0F,false);
					}
				}else{
					if( AngleDir(vertexDirection,sourceDirection,Vector3.up)>0F ){
						Debug.DrawLine(poly[ip].vertices[iv], poly[ip].vertices[nv], Color.white,0F,false);
					}
				}
			}
		}
	}
	
	//check if a point is left or right to a direction vector, can be reduced for 2D only
	private float AngleDir(Vector3 fwd, Vector3 targetDir, Vector3 up) {
		Vector3 perp = Vector3.Cross(fwd, targetDir);
		float dir	 = Vector3.Dot(perp, up);
		if		(dir > 0F)	{ return  1F;}//RIGHT
		else if	(dir < 0F)	{ return -1F;}//LEFT
		else				{ return  0F;}
	}
	#endregion
		
	#region POLYGON EXTRACTION
	private	void GeneratePolygonStructArr(ref BottomPolygon[] poly){
		poly = new BottomPolygon[walls.Length];
		
		int iPoly = 0;	//polygon integrator
		foreach(GameObject wall in walls){
			
			//Save Transform reference
			poly[iPoly].transform = wall.transform;
			
			Mesh mesh = wall.GetComponent<MeshFilter>().mesh;
			Vector3[] vertices = mesh.vertices;
			int[] triangles = mesh.triangles;
			
			Vector3[] normals = mesh.normals;
			
			if(worldNormals){
				for(int i = 0; i < vertices.Length; i++){
					normals[i] = poly[iPoly].transform.TransformDirection(mesh.normals[i]);
				}
			}		
					
		//SIZECHECK, list usage could get rid of this step
			//check how much valid vertex are present to assign array lengths in the next step
			int validVertices = 0;
			for(int i = 0; i < vertices.Length; i++){
				//BOTTOM, or which orthogonal direction you need... e.g. sidescroller: if mesh.normals.z-1
				if(normals[i].y == -1){	//if the normal of this vertice is pointing down, should be only 4 vertices per rectangle
					validVertices++;
				}
			}
									
		//BOTTOM-VERTICES of the walls bottom-plane
			poly[iPoly].vertices = new Vector3[validVertices];	//init new Vector3 array of the struct with the needed length
			int[] validIndices = new int[validVertices];					//the original indices of the valid vertices, used to find the right triangles
			Vector3[] bottomVertices = new Vector3[validVertices];
			//int[] newIndices = new int[validVertices];						//new indices of the vertices, used to map newTriangles
			
			
			//save the valid vertices and triangles of the current wall
			int iv = 0;	//array integrator
			for(int i = 0; i < vertices.Length; i++){	//for ALL vertices of the wall mesh
				if(normals[i].y == -1){	//if the normal of this vertice is pointing down, e.g. should be only 4 vertices per rectangle
					//actual saving of the vertex in WORLD COORDINATES
					bottomVertices[iv] = wall.transform.TransformPoint(vertices[i]);
					validIndices[iv] = i;
					//newIndices[iv] = iv;
					iv++;
				}
			}
			
			if(validIndices.Length == 0){break;}//early out
		
		//BOTTOM-TRIANGLES of one poly, maybe we dont need them directly later, but here they are needed to delete inner vertices (e.g. center of cylinder vertex)
			List<int> bottomTrianglesList = new List<int>();	//using the OLD indices
			int iAs = 0; //iterator for assigned triangles
			for(int it = 0; it < triangles.Length;){// iterator triangles
				//check if the next 3 indices of triangles match
				int match = 0;//check the next 3 indices of this triangles
				for(int imatch = 0; imatch<3; imatch++){
					for(int ivv = 0; ivv < validIndices.Length; ivv++){//check with all vertices
						if(validIndices[ivv]==triangles[it+imatch]){
							match++;
						}
					}
				}
				//if all 3 indices of a triangle match with the validIndices, it is a bottom triangle
				if(match == 3){ //create new triangle in list
					bottomTrianglesList.Add(triangles[it+0]);
					bottomTrianglesList.Add(triangles[it+1]);
					bottomTrianglesList.Add(triangles[it+2]);
					iAs += 3; //assign iterator rdy for next triangle
				}
				it+=3;//next triangle
			}
			//now we have all triangles that are contained in the bottom plane, but with the original indices
						
			int[] bottomTrianglesArr = bottomTrianglesList.ToArray();
			int[] bottomTriangles = new int[bottomTrianglesArr.Length];	//using the OLD indices
			//Update indices to refer to bottomVertices:
			for(int ib = 0; ib < bottomTrianglesArr.Length; ib++){
				for(int ivi = 0; ivi < validIndices.Length; ivi++){	//check for original index, assign corresponding new index, must hit once per loop!
					if(bottomTrianglesArr[ib] == validIndices[ivi]){
						bottomTriangles[ib] = ivi;//currently the same as newTriangles[ib] = newIndices[ivi];, we dont need newIndices[]
					}
				}
			}
					
//AT THIS POINT we have the bottom vertices and triangles of the bottomPlane rdy for any use
//			bottomVertices & bottomTriangles
			
			//Now we have to find the outlining polygon:			
			ExtractPolygon(bottomVertices, bottomTriangles, ref poly[iPoly]);	//extracts polygon and saves it directly in passed poly struct
			
			
						//OTHER assignments for future purpose
							//add and save visibilityFader Reference and set Blackness of the Fader
							if(!wall.GetComponent<VisibilityFader>()){
								poly[iPoly].fader = wall.AddComponent<VisibilityFader>();
								poly[iPoly].fader.ManualInit();
							}else{
								poly[iPoly].fader = wall.GetComponent<VisibilityFader>();
							}
							poly[iPoly].fader.SetBlackness(0.5F);
										
							//Not implemented yet
							//poly[iPoly].position = CalculateCenter(poly[iPoly].vertices);
							//poly[iPoly].relevantRadius = CalculateRadius(poly[iPoly].vertices);
						//OTHER END

			iPoly++;
		}
	}
	
	//GeneratePolygonStructArr-helper, deletes inner vertices, returns new vertices, no triangles because it would make no sense without inner vertices
	private Vector3[] DeleteInnerVertices(Vector3[] vertices, int[] triangles){
		List<Vector3> outerVertices = new List<Vector3>();
		for(int iv = 0; iv<vertices.Length; iv++){//for all vertices
			int matches = 0;
			for(int it = 0; it < triangles.Length; it++){
				if(triangles[it] == iv){//check how often this vertex-index appears in the triangles
					matches++;
				}
			}
			//Assumption:
			if(matches < 3){//if vertex is used in less than 3 triangles
				//then this is an outer vertex, add it to list
				outerVertices.Add(vertices[iv]);
			}
		}
		//create array
		return outerVertices.ToArray();
	}
	
	private void ExtractPolygon(Vector3[] vertices, int[] triangles, ref BottomPolygon poly){		
		//definitions used (see http://www.geosensor.net/papers/duckham08.PR.pdf)
		//→A triangulation ∆ is a combinatorial map which has the property that every edge in a set of edges belongs to either one or two triangle s
		//→Aboundary edge of ∆ is an edge that belongs to exactly one triangle in ∆.
		
		//for simple polygons(edges do not cross themselfes) which we are dealing with,
		//the outline polygon consists out of the edges that appear only in one triangle:
		
		//earlyOutTest
		//poly.vertices	= vertices;	return;
		
		List<int[]> allEdges = new List<int[]>();					//list of 2 integers each representing the index of a vertex
		List<int[]> unsortedBE = new List<int[]>();					//unsortedBoundaryEdges
		List<int[]> boundaryEdges = new List<int[]>();				//sorted outer edges
		
		List<Vector3> boundaryVertices = new List<Vector3>();	//the vertices of the polygon in ccw order, this is what we need!
		
		//get all edges
		for(int it = 0; it<triangles.Length;){//for all triangles, add their adges to the list
			allEdges.Add(new int[2]{triangles[it+0],triangles[it+1]});	//edge1
			allEdges.Add(new int[2]{triangles[it+1],triangles[it+2]});	//edge2
			allEdges.Add(new int[2]{triangles[it+2],triangles[it+0]});	//edge3
			it+=3;
		}//Debug.Log("Edges:"+allEdges.Count);
		
		//DROP all edges that appear in more than one triangle
		for( int iT = 0; iT < allEdges.Count; iT++){	//for each edge
			int o = iT%3;	//offset to find the edges that belong to the same triangle
			bool addEdge = true;
			for(int iC = 0; iC < allEdges.Count; iC++){	//compare loop, check all other edges
				if(	!((iT+0-o) == iC	||	(iT+1-o) == iC	||	(iT+2-o) == iC)	){	//except edges of current triangle from check
					if(		(allEdges[iT][0] == allEdges[iC][0] && allEdges[iT][1] == allEdges[iC][1]) || 
							(allEdges[iT][0] == allEdges[iC][1] && allEdges[iT][1] == allEdges[iC][0]) ){
						addEdge = false;
						break;
					}
				}
			}
			//if this edge has not appeared twice we can add it to our boundary edge List
			if(addEdge){	unsortedBE.Add(allEdges[iT]);	}
		}//Debug.Log("Edges:"+unsortedBE.Count);
		
		//SORT unsortedBE, no edge will be dropped now, indices of each edge may be swapped
		//→	unsorted List:
		//	edge1		edge2		edge3		edge4
		//	[4][2]		[0][1]		[1][4]		[0][2]
		
		//→	sorted List:
		//	edge1		edge2(4)	edge3(2)	edge4(3)
		//	[4][2]		[2][0]		[0][1]		[1][4]
				
		//add first edge to start:
		boundaryEdges.Add(unsortedBE[0]);
		int failsave = 100;	//if bottomplane is faulty
		for(int iList = 1; iList < unsortedBE.Count;){	//compare loop, one edge has to match each run (closed edge loop)
			for(int iC = 1; iC < unsortedBE.Count; iC++){	//check all edges but the first (already added)	
				//check if last index matches with another index, then add it to the sorted list
				//Debug.Log(boundaryEdges[iList-1][1] +"|"+ unsortedBE[iC][0]);
				if( boundaryEdges[iList-1][1] == unsortedBE[iC][0] ){	//common vertex on compare-edge[0], add!
					boundaryEdges.Add(new int[2]{unsortedBE[iC][0],unsortedBE[iC][1]});
					iList++;
				}else if( boundaryEdges[iList-1][1] == unsortedBE[iC][1] ){	//common vertex on compare-edge[1], add swapped!
					boundaryEdges.Add(new int[2]{unsortedBE[iC][1],unsortedBE[iC][0]});
					iList++;					
				}
			}
			
			if(failsave<1){
				Debug.Log("Aborted Loop, bottomplane faulty!");
				break;
			}
			failsave--;
		}
			
		//Finally! generate the vertices of the polygon out of the sorted list
		foreach(int[] intArr in boundaryEdges){
			//just add one side([0] or [1]) of each element in the sorted List and we have the vertices in right order
			boundaryVertices.Add(vertices[intArr[0]]);
		}
		
		if(checkCCW){
			FixCCWOrder(ref boundaryVertices);
		}
		
		//put this in boundaryVertices assignment if always needed
		if(clampY){
			for(int iV = 0; iV<boundaryVertices.Count; iV++){
				boundaryVertices[iV] = new Vector3(boundaryVertices[iV].x,0F,boundaryVertices[iV].z);
			}
		}
				
		poly.vertices = boundaryVertices.ToArray();
	}
	
	//checking order of vertices via angle sum
	private	void FixCCWOrder(ref List<Vector3> vertices){
		//the list could be CW instead of CCW, check it by checking angle sum
		//→inner angles is always smaller than outer angles
		float angleSumInc = 0F;	//the angleSum of the polygon in one direction
		float angleSumDec = 0F;	//the angleSum of the polygon in the other direction
		for(int iV = 0; iV<vertices.Count; iV++){
			int nV	= (iV+1)%vertices.Count; //next vertice
			int nV2	= (iV+2)%vertices.Count; //next vertice
			//get Direction to next point, we need 2 direction to calc the angle in between
			Vector3 dir1 = (vertices[iV] -vertices[nV]);	//direction from next vertex to current Vertex
			Vector3 dir2 = (vertices[nV2]-vertices[nV]);	//direction from next vertex to next next Vertex
			//Debug.DrawRay(vertices[nV],dir1*1.2F,Color.red,0.5F,false);
			//Debug.DrawRay(vertices[nV],dir2*1.2F,Color.yellow,1F,false);
			float angle = Vector3.Angle(dir1,dir2);//always shortest angle in 3D space!
			//therefore check if we have a left turn or right turn
			if(AngleDir(dir1,dir2,Vector3.up)>0F){
				angleSumInc += angle;
				angleSumDec += 360F-angle;
			}else{
				angleSumInc += 360F-angle;
				angleSumDec += angle;
			}	
		}//Debug.Log("AngleSum:\t\tinc:\t"+angleSumInc +"\n\t\t\t\t\tdec:\t"+angleSumDec);
		
		//i order is reversed, fix it!
		if(angleSumInc>angleSumDec){vertices.Reverse();}
	}
	
	//same as above but for arrays rather than lists
	private	Vector3[] FixCCWOrder(Vector3[] vertices){
		float angleSumInc = 0F;
		float angleSumDec = 0F;
		for(int iV = 0; iV<vertices.Length; iV++){
			int nV	= (iV+1)%vertices.Length;
			int nV2	= (iV+2)%vertices.Length;
			Vector3 dir1 = (vertices[iV] -vertices[nV]);
			Vector3 dir2 = (vertices[nV2]-vertices[nV]);
			float angle = Vector3.Angle(dir1,dir2);
			if(AngleDir(dir1,dir2,Vector3.up)>0F){
				angleSumInc += angle;
				angleSumDec += 360F-angle;
			}else{
				angleSumInc += 360F-angle;
				angleSumDec += angle;
			}	
		}
		if(angleSumInc>angleSumDec){
			Vector3[] reversedVertices = new Vector3[vertices.Length];
			for(int iV = 0; iV<vertices.Length; iV++){
				reversedVertices[iV] = vertices[vertices.Length-iV-1];
			}
			return reversedVertices;
		}
		return vertices;
	}
	
	//mybe needed... http://stackoverflow.com/questions/5271583/center-of-gravity-of-a-polygon
	private	Vector3 CalculateCenter(ref Vector3[] polygon){
		return Vector3.zero;
	}
	
	//smallest circle problem
	private	float CalculateRadius(ref Vector3[] polygon){
		return 0F;
	}
	#endregion
	
	#region RANDOM POLYGON
	
	private	void CreateRandomPolygon(ref BottomPolygon[] poly){
		BottomPolygon[] enlargedArray = new BottomPolygon[poly.Length+1];
		//just a quick and dirty distorted circle... = simple polygon, convex or concave possible
		int edgeCount = Random.Range(3,20);
		float radius  = Random.Range(10F,30F);
		Vector3 pos = new Vector3(Random.Range(-220F,220F),0F,Random.Range(-220F,220F));
		List<Vector3> newPoly = new List<Vector3>();
		
		//create some random angles
		List<float> angles = new List<float>();	
		for(int i = 0; i < edgeCount; i++){
			float angle = Random.Range(0F,2F*Mathf.PI);
			angles.Add(angle);
		}
		
		//sort Array by either des or asc to test FixCCWOrder
		if(RandBool()){
			angles.Sort((p1, p2) => (p1.CompareTo(p2)));//Debug.Log("normal order");
		}else{
			angles.Sort((p2, p1) => (p1.CompareTo(p2)));//Debug.Log("reversed order");
		}
		
		for(int i = 0; i < edgeCount; i++){
			float dis = Random.Range(0.2F,1F)*radius;	//distance to center
			newPoly.Add(new Vector3(Mathf.Sin(angles[i])*dis,0F,Mathf.Cos(angles[i])*dis) +pos);
		}
		
		//copy old Polys to Polygon Array
		for(int i = 0; i< enlargedArray.Length-1; i++){
			enlargedArray[i].vertices = poly[i].vertices;
		}
		
		//check Order
		if(checkCCW){
			FixCCWOrder(ref newPoly);
		}
		
		//add new poly at last position
		enlargedArray[enlargedArray.Length-1].vertices = newPoly.ToArray();
				
		//save new array
		poly = enlargedArray;
	}
	
	private	void ClearRandomPolygons(ref BottomPolygon[] polyArr){
		polyArr = new BottomPolygon[0];
	}
	
	private	void RefreshRandPolygons(ref BottomPolygon[] polyArr){
		//check Order
		if(checkCCW){
			for(int i = 0; i < polyArr.Length; i++){
				polyArr[i].vertices = FixCCWOrder(polyArr[i].vertices);
			}
		}
	}
	
	private	bool RandBool(){return (Random.value > 0.5f);}//just a random bool
	#endregion
	
	
	#region UTILITY
	//camera panning variables
	private	Vector3 curOrigin;
	private	Vector3 camOrigin;
	private	Vector3 lastPos;
	private	void CameraManipulation(){
		if(!mouseOver){
			RaycastHit hit;
			Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition),out hit);
			Vector3 pos = hit.point;
				if(Input.GetKeyDown("mouse 1")){lastPos = pos;}
				if(Input.GetKey("mouse 1")){
					cam.transform.position = (camOrigin + (curOrigin - lastPos));
					//renew point
					Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition),out hit);
					lastPos		= hit.point;
					camOrigin	= cam.transform.position;
				}else{
					camOrigin	= cam.transform.position;
					curOrigin	= pos;
					if(Input.GetKey("mouse 0")){
						source.position = pos;
					}
				}
			source.position = new Vector3(source.position.x,0F,source.position.z);
		}
		
		if (Input.GetAxis("Mouse ScrollWheel") < 0){	// back, zoom out
			if(cam.orthographicSize * 1.25F < 500F){
				cam.orthographicSize *= 1.25F;
			}
		}else if (Input.GetAxis("Mouse ScrollWheel") > 0){	// forward, zoom in
			if(cam.orthographicSize * 1.25F > 12F){
				cam.orthographicSize *= 0.8F;
			}
		}
	}
	
	private	void FaderTest(ref BottomPolygon[] poly){
		for(int ip = 0; ip < poly.Length; ip++){	//polygon
			if(poly[ip].fader){
				poly[ip].fader.FadeIn();
			}
		}
	}
	
	//GUI Utility
	private	bool drawLineToVertices	= true;
	private	bool drawPolygons		= true;
	private	bool drawVisibleFaces	= true;
	private	bool drawSegments		= false;
	private	bool drawLines			= false;
	private	bool drawCuts			= false;
	private	bool drawRecalc			= false;
	private	bool drawFinal			= false;
	
	private	bool fadeTest			= false;
	private	bool checkCCW			= true;
	private	bool worldNormals		= false;
	private	bool invisibleFaces		= false;
	private	bool autoGen			= false;
	private	bool clampY				= false;
	
	private	int  polyCount			= 0;
	
	public	string lastTooltip = " ";
	private	bool mouseOver = false;
	
	private	void OnGUI(){
		//Quick GUI, just copied it from somthing and inserted the right bools
		int	bh	= 22;
		int	y	= 10;
		int	dy	= 22;
		int	bl	= 120;
		int	off = 135;
		GUI.skin.button.fontSize = 11;
		GUI.skin.toggle.fontSize = 11;
		GUI.skin.label .fontSize = 12;
		
	//Controls
		GUI.skin.label .fontSize = 22;
		GUI.Label (new Rect(Screen.width-230,y,230,bh*4),"Controls");
		GUI.skin.label .fontSize = 12;
		GUI.Label (new Rect(Screen.width-225,y*4,230,bh*4),"" +
			"Light Position:\tLeft Mouse\n" +
			"Zoom:\tMouseWheel\n"+
			"Pan:\t\tHold RightMouseButton\n\n"+
			"ENABLE GIZMOS!");
		
		GUI.Label (new Rect(10,y,600,bh),"Create Random Polygon ["+polyCount+"]"); y+=dy;
		if(GUI.Button (new Rect(10,	y, bl/2, bh),	new GUIContent("Create",	"cr")))	{CreateRandomPolygon(ref randomPolys);}
		if(GUI.Button (new Rect(10+bl/2,y, bl/2, bh),	new GUIContent("Clear",	"cl")))	{ClearRandomPolygons(ref randomPolys);}y+=dy;
		if(GUI.Button (new Rect(10,	y, bl, bh),	new GUIContent("Auto",		"13")))	{autoGen = !autoGen;}
		autoGen	= (GUI.Toggle (new Rect(off,	y, 70, bh),	autoGen, ""));
		
		y+=dy;
		y+=dy;
		
		GUI.Label (new Rect(10,y,400,bh),"Draw:"); y+=dy;
		if(GUI.Button (new Rect(10,	y, bl, bh),	new GUIContent("LineToVertices","1")))	{drawLineToVertices = !drawLineToVertices;}y+=dy;
		if(GUI.Button (new Rect(10,	y, bl, bh),	new GUIContent("Polygons",		"2")))	{drawPolygons = !drawPolygons;}y+=dy;
		if(GUI.Button (new Rect(10,	y, bl, bh),	new GUIContent("VisibleFaces",	"3")))	{drawVisibleFaces = !drawVisibleFaces;}y+=dy;
		GUI.enabled = false;
		if(GUI.Button (new Rect(10,	y, bl, bh),	new GUIContent("Segments",		"4")))	{drawSegments = !drawSegments;}y+=dy;
		if(GUI.Button (new Rect(10,	y, bl, bh),	new GUIContent("Lines",			"5")))	{drawLines = !drawLines;}y+=dy;
		if(GUI.Button (new Rect(10,	y, bl, bh),	new GUIContent("Cuts",			"6")))	{drawCuts = !drawCuts;}y+=dy;
		if(GUI.Button (new Rect(10,	y, bl, bh),	new GUIContent("Recalc",		"7")))	{drawRecalc = !drawRecalc;}y+=dy;
		if(GUI.Button (new Rect(10,	y, bl, bh),	new GUIContent("Final",			"8")))	{drawFinal = !drawFinal;}y+=dy;
		GUI.enabled = true;
		
		//indicators
		y-=dy*8;
		GUI.enabled = false;
		drawLineToVertices	= (GUI.Toggle (new Rect(off,	y, 70, bh),	drawLineToVertices, ""));	y+=dy;
		drawPolygons		= (GUI.Toggle (new Rect(off,	y, 70, bh),	drawPolygons, ""));			y+=dy;
		drawVisibleFaces	= (GUI.Toggle (new Rect(off,	y, 70, bh),	drawVisibleFaces, ""));		y+=dy;
		drawSegments		= (GUI.Toggle (new Rect(off,	y, 70, bh),	drawSegments, ""));			y+=dy;
		drawLines			= (GUI.Toggle (new Rect(off,	y, 70, bh),	drawLines, ""));			y+=dy;
		drawCuts			= (GUI.Toggle (new Rect(off,	y, 70, bh),	drawCuts, ""));				y+=dy;
		drawRecalc			= (GUI.Toggle (new Rect(off,	y, 70, bh),	drawRecalc, ""));			y+=dy;
		drawFinal			= (GUI.Toggle (new Rect(off,	y, 70, bh),	drawFinal, ""));			y+=dy;
		GUI.enabled = true;
		
		
		GUI.Label (new Rect(10,y,400,bh),"Polygon Extraction/Generation:"); y+=dy;
		
		y+=dy;
		if(GUI.Button (new Rect(10,	y, bl, bh),	new GUIContent("Refresh",		"9")))	{GeneratePolygonStructArr(ref staticBlockers); RefreshRandPolygons(ref randomPolys);}
		y+=dy;
		if(GUI.Button (new Rect(30,	y, bl-20, bh),	new GUIContent("↘checkCCW",		"10")))	{checkCCW = !checkCCW;}
		checkCCW	= (GUI.Toggle (new Rect(off,	y, 70, bh),	checkCCW, ""));	y+=dy;
		
		if(GUI.Button (new Rect(30,	y, bl-20, bh),	new GUIContent("↘worldNormals",		"11")))	{worldNormals = !worldNormals;}
		worldNormals	= (GUI.Toggle (new Rect(off,	y, 70, bh),	worldNormals, ""));	y+=dy;
		
		if(GUI.Button (new Rect(30,	y, bl-20, bh),	new GUIContent("↘invisibleFaces",		"12")))	{invisibleFaces = !invisibleFaces;}
		invisibleFaces	= (GUI.Toggle (new Rect(off,	y, 70, bh),	invisibleFaces, ""));	y+=dy;
		
		if(GUI.Button (new Rect(30,	y, bl-20, bh),	new GUIContent("↘clampY to 0",		"13")))	{clampY = !clampY;}
		clampY	= (GUI.Toggle (new Rect(off,	y, 70, bh),	clampY, ""));	y+=dy;
		
		
		y+=dy;
		y+=dy;
		
		GUI.Label (new Rect(10,y,400,bh),"Other Stuff:"); y+=dy;
		
		if(GUI.Button (new Rect(10,	y, bl, bh),	new GUIContent("TestFader",		"99")))	{fadeTest = !fadeTest;}
		fadeTest	= (GUI.Toggle (new Rect(off,	y, 70, bh),	fadeTest, ""));
		
		//Mouse above GUI check
		if (Event.current.type == EventType.Repaint && GUI.tooltip != lastTooltip) {
			if (lastTooltip != "")
				SendMessage("OnMouseOut", SendMessageOptions.DontRequireReceiver);
			
			if (GUI.tooltip != "")
				SendMessage("OnMouseOver", SendMessageOptions.DontRequireReceiver);
			lastTooltip = GUI.tooltip;
		}
		//↘↓↙←↖↑↗→
	}
	
	private	void OnMouseOver()	{ mouseOver	=  true;}
	private	void OnMouseOut()	{ mouseOver	= false;}
	
	#endregion
}
