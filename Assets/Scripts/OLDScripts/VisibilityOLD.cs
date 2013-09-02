using UnityEngine;
using System.Collections;

using System.Collections.Generic;

public class VisibilityOLD : MonoBehaviour {
	#region 2ND APPROACH
	//REDBOXGAMES
	// Calculate visible area from a position
	// Copyright 2012 Red Blob Games
	// License: Apache v2
	
	/*
	   This code also uses a linked list datastructure class from
	   Polygonal, which is Copyright (c) 2009-2010 Michael Baczynski,
	   http://www.polygonal.de. It is available under the new BSD license,
	   except for two algorithms, which I do not use. See
	   https://github.com/polygonal/polygonal/blob/master/LICENSE
	*/
	
//	import de.polygonal.ds.DLL;
	
//	typedef Block = {x:Float, y:Float, r:Float};
//	typedef Point = {x:Float, y:Float};
//	typedef Segment = {p1:EndPoint, p2:EndPoint, d:Float};
//	typedef EndPoint = {x:Float, y:Float, begin:Bool, segment:Segment, angle:Float, visualize:Bool};
	
	public class Block{
		public	float x;
		public	float y;
		public	float r;
	}
	
	public class Point{	//=vector2
		public	float x;
		public	float y;
	}
	
	public class Segment{
		public	EndPoint p1;
		public	EndPoint p2;
		public	float d;
	}
	
	public class EndPoint{
		public	float x;
		public	float y;
		public	bool  begin;
		public	Segment segment;
		public	float angle;
		public	bool visualize;
		
		/*public void Init(Point p1, Point p2, bool  begin, Segment segment, float angle, bool visualize){
			this.p1 = p1;
			this.p2 = p2;
			this.begin = begin;
			this.segment = segment;
			this.angle = angle;
			this.visualize = visualize;
		}*/
	}
	
	/* 2d visibility algorithm, for demo
	   Usage:
		  new Visibility()
	   Whenever map data changes:
		  loadMap
	   Whenever light source changes:
		  setLightLocation
	   To calculate the area:
		  sweep
	*/
	
//	@:expose @:keep class Visibility {
		// Note: DLL is a doubly linked list but an array would be ok too
	
		// These represent the map and the light location:
//		public var segments:DLL<Segment>;
		private	List<Segment> segments = new List<Segment>();
//		public var endpoints:DLL<EndPoint>;
		private	List<EndPoint> endpoints = new List<EndPoint>();
//		public var center:Point;
		private	Point center;
	
		// These are currently 'open' line segments, sorted so that the nearest
		// segment is first. It's used only during the sweep algorithm, and exposed
		// as a public field here so that the demo can display it.
//		public var open:DLL<Segment>;
		private	List<Segment> open = new List<Segment>();

		// The output is a series of points that forms a visible area polygon
//		public var output:Array<Point>;
		private List<Point> output = new List<Point>();
	
		// For the demo, keep track of wall intersections
//		public var demo_intersectionsDetected:Array<Array<Point>>;
		//private List<List<Point>> demo_intersectionsDetected = new List<List<Point>>();
	
		// Construct an empty visibility set
//		public function new() {
//			segments = new DLL<Segment>();
//			endpoints = new DLL<EndPoint>();
//			open = new DLL<Segment>();
//			center = {x: 0.0, y: 0.0};
//			output = new Array();
//			demo_intersectionsDetected = [];
//		}
	
	
		private void Start(){
		}

		Vector3 sPos = Vector3.zero;
		private void Update(){
			//sPos = GetComponent<GeometricVisibility>().source.position;
			//center = new Point{x = sPos.x, y = sPos.y};
		}
	
	
		public void Flush(){
			segments.Clear();//segments.clear();
			endpoints.Clear();//endpoints.clear();
		}
	
		//initiated from Geometric Visibility
		Vector3 off;//Drawline viewpoint
		public void InitCalc(Vector3 sPos){
			this.sPos = sPos;	
			
			//List<Block> block = new List<Block>();
			//block.Add(new Block{x = 10F, y = 10F, r = 45F});
		
			loadEdgeOfMap(100, 200);
		
		//points are given in world coordinates
			//setLightLocation(sPos.x, sPos.z);
			
		//points are relative to source coordinates
			setLightLocation(0F, 0F);	
			off = sPos;	//Debug.Log(off);
		
			//DrawEndpoints(ref endpoints);
			
			//Debug.Log("I"+endpoints.Count);		
			sweep();
		
			//Debug.Log("O"+output.Count);
		
			//DrawOutput(ref output);	
		
			//maxDistance = 100f;
			ConvertOutput(ref output);		//create poly
					
			Debug.DrawLine(new Vector3(-3F,0F,center.y),new Vector3(+3F,0F,center.y),new Color(1F,1F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(center.x,0F,-3F),new Vector3(center.x,0F,+3F),new Color(1F,1F,1F,0.5F),0F,false);
		}
				
		// Helper function to construct segments along the outside perimeter
		private void loadEdgeOfMap(int size, int margin) {
			//addSegment(margin, margin, margin, size-margin);
			//addSegment(margin, size-margin, size-margin, size-margin);
			//addSegment(size-margin, size-margin, size-margin, margin);
			//addSegment(size-margin, margin, margin, margin);
			
			float lenght = 150F;
		
			Vector3 o = Vector3.zero;//moving borders
			//Vector3 o = -sPos;//fixed borders
		
		
			addSegment( lenght+o.x,  lenght+o.z,  lenght+o.x, -lenght+o.z);
			addSegment( lenght+o.x, -lenght+o.z, -lenght+o.x, -lenght+o.z);
			addSegment(-lenght+o.x, -lenght+o.z, -lenght+o.x,  lenght+o.z);
			addSegment(-lenght+o.x,  lenght+o.z,  lenght+o.x,  lenght+o.z);
		
			// NOTE: if using the simpler distance function (a.d < b.d)
			// then we need segments to be similarly sized, so the edge of
			// the map needs to be broken up into smaller segments.
		}
	
		
		// Load a set of square blocks, plus any other line segments
		public void loadMap(int size, int margin, List<Block> blocks, List<Segment> walls) {
			//segments.Clear();//segments.clear();
			//endpoints.Clear();//endpoints.clear();
			loadEdgeOfMap(size, margin);
			
			foreach (Block block in blocks) {
				float x = block.x;
				float y = block.y;
				float r = block.r;
				addSegment(x-r, y-r, x-r, y+r);
				addSegment(x-r, y+r, x+r, y+r);
				addSegment(x+r, y+r, x+r, y-r);
				addSegment(x+r, y-r, x-r, y-r);
			}
			foreach (Segment wall in walls) {
				addSegment(wall.p1.x, wall.p1.y, wall.p2.x, wall.p2.y);
			}
		}
	
	
		// Add a segment, where the first point shows up in the
		// visualization but the second one does not. (Every endpoint is
		// part of two segments, but we want to only show them once.)
		public void addSegment(float x1, float y1, float x2, float y2) {
			Segment segment = new Segment();//null;
			//EndPoint p1 = {begin, x, y, angle,segment, visualize};
		
			//EndPoint p1 = new EndPoint.Init(begin = false, x = 0F, y= 0F, angle = 0F,segment = segment, visualize = true);
			//EndPoint p2 = new EndPoint.Init(begin = false, x = 0F, y= 0F, angle = 0F,segment = segment, visualize = false);
			
			EndPoint p1 = new EndPoint{begin = false, x = 0F, y = 0F, angle = 0F,segment = segment, visualize = true};
			EndPoint p2 = new EndPoint{begin = false, x = 0F, y = 0F, angle = 0F,segment = segment, visualize = false};
			//EndPoint p2 = {begin: false, x: 0.0, y: 0.0, angle: 0.0,segment: segment, visualize: false};
			//segment = {p1: p1, p2: p2, d: 0.0};
			p1.x = x1; p1.y = y1;
			p2.x = x2; p2.y = y2;
			p1.segment = segment;
			p2.segment = segment;
			segment.p1 = p1;
			segment.p2 = p2;
		
			segments.Add(segment);	//segments.append(segment);
			endpoints.Add(p1);	//endpoints.append(p1);
			endpoints.Add(p2);	//endpoints.append(p2);
		
			//Drawline lags one frame behind because off is updated after, no problem
			//Debug.DrawLine(new Vector3(p1.x,0F,p1.y)+off,new Vector3(p2.x,0F,p2.y)+off,new Color(1F,1F,1F,0.5F),0F,false);
		}
	
	
		// Set the light location. Segment and EndPoint data can't be
		// processed until the light location is known.
		public void setLightLocation(float x, float y) {
			center = new Point{x = x, y = y};
			//center.x = x;
			//center.y = y;
			
			foreach (Segment segment in segments) {
				float dx = 0.5F * (segment.p1.x + segment.p2.x) - x;
				float dy = 0.5F * (segment.p1.y + segment.p2.y) - y;
				// NOTE: we only use this for comparison so we can use
				// distance squared instead of distance
				segment.d = dx*dx + dy*dy;
	
				// NOTE: future optimization: we could record the quadrant
				// and the y/x or x/y ratio, and sort by (quadrant,
				// ratio), instead of calling atan2. See
				// <https://github.com/mikolalysenko/compare-slope> for a
				// library that does this.
				segment.p1.angle = Mathf.Atan2(segment.p1.y - y, segment.p1.x - x);
				segment.p2.angle = Mathf.Atan2(segment.p2.y - y, segment.p2.x - x);
	
				var dAngle = segment.p2.angle - segment.p1.angle;
				if (dAngle <= -Mathf.PI) { dAngle += 2*Mathf.PI; }
				if (dAngle > Mathf.PI) { dAngle -= 2*Mathf.PI; }
				segment.p1.begin = (dAngle > 0.0);
				segment.p2.begin = !segment.p1.begin;
			}
		}
	
	
		// Helper: comparison function for sorting points by angle
		static private int _endpoint_compare(EndPoint a, EndPoint b) {
			// Traverse in angle order
			if (a.angle > b.angle) return 1;
			if (a.angle < b.angle) return -1;
			// But for ties (common), we want Begin nodes before End nodes
			if (!a.begin && b.begin) return 1;
			if (a.begin && !b.begin) return -1;
			return 0;
		}
	
	
		//NO INLINE in C# afaik, manually inline function or trust compiler to inline it
	
		// Helper: leftOf(segment, point) returns true if point is
		// "left" of segment treated as a vector
		static /*inline*/ private bool leftOf(Segment s, Point p){
			var cross = (s.p2.x - s.p1.x) * (p.y - s.p1.y)
					  - (s.p2.y - s.p1.y) * (p.x - s.p1.x);
			return cross < 0;
		}
	
		// Return p*(1-f) + q*f
		static private Point interpolate(EndPoint p, EndPoint q, float f){
			return new Point{x = p.x*(1-f) + q.x*f, y = p.y*(1-f) + q.y*f};
		}
		
		// Helper: do we know that segment a is in front of b?
		// Implementation not anti-symmetric (that is to say,
		// _segment_in_front_of(a, b) != (!_segment_in_front_of(b, a)).
		// Also note that it only has to work in a restricted set of cases
		// in the visibility algorithm; I don't think it handles all
		// cases. See http://www.redblobgames.com/articles/visibility/segment-sorting.html
		private bool intersectionsOccured = false;
		private float shrt = 0.01F; //shortening distance
		private bool _segment_in_front_of(Segment a, Segment b, Point relativeTo) {
			// NOTE: we slightly shorten the segments so that
			// intersections of the endpoints (common) don't count as
			// intersections in this algorithm
			bool A1 = leftOf(a, interpolate(b.p1, b.p2, shrt));
			bool A2 = leftOf(a, interpolate(b.p2, b.p1, shrt));
			bool A3 = leftOf(a, relativeTo);
			bool B1 = leftOf(b, interpolate(a.p1, a.p2, shrt));
			bool B2 = leftOf(b, interpolate(a.p2, a.p1, shrt));
			bool B3 = leftOf(b, relativeTo);
	
			// NOTE: this algorithm is probably worthy of a short article
			// but for now, draw it on paper to see how it works. Consider
			// the line A1-A2. If both B1 and B2 are on one side and
			// relativeTo is on the other side, then A is in between the
			// viewer and B. We can do the same with B1-B2: if A1 and A2
			// are on one side, and relativeTo is on the other side, then
			// B is in between the viewer and A.
			if (B1 == B2 && B2 != B3) return true;
			if (A1 == A2 && A2 == A3) return true;
			if (A1 == A2 && A2 != A3) return false;
			if (B1 == B2 && B2 == B3) return false;
			
			// If A1 != A2 and B1 != B2 then we have an intersection.
			// Expose it for the GUI to show a message. A more robust
			// implementation would split segments at intersections so
			// that part of the segment is in front and part is behind.
			//demo_intersectionsDetected.push([a.p1, a.p2, b.p1, b.p2]);
		
		
			intersectionsOccured = true;
			//show intersecting Segment
			Vector3 a1 = new Vector3(a.p1.x, 0F, a.p1.y)+off;
			Vector3 a2 = new Vector3(a.p2.x, 0F, a.p2.y)+off;
			Vector3 b1 = new Vector3(b.p1.x, 0F, b.p1.y)+off;
			Vector3 b2 = new Vector3(b.p2.x, 0F, b.p2.y)+off;
			Debug.DrawLine(a1,a2,Color.magenta,0F,false);
			Debug.DrawLine(b1,b2,Color.cyan,0F,false);
		
			float l = 0.4F;
			Debug.DrawLine(new Vector3(a1.x-l,0F,a1.z),new Vector3(a1.x+l,0F,a1.z),new Color(1F,0F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(a1.x,0F,a1.z-l),new Vector3(a1.x,0F,a1.z+l),new Color(1F,0F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(a2.x-l,0F,a2.z),new Vector3(a2.x+l,0F,a2.z),new Color(1F,0F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(a2.x,0F,a2.z-l),new Vector3(a2.x,0F,a2.z+l),new Color(1F,0F,1F,0.5F),0F,false);
		
		
			Debug.DrawLine(new Vector3(b1.x-l,0F,b1.z-l),new Vector3(b1.x+l,0F,b1.z+l),new Color(0F,1F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(b1.x+l,0F,b1.z-l),new Vector3(b1.x-l,0F,b1.z+l),new Color(0F,1F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(b2.x-l,0F,b2.z-l),new Vector3(b2.x+l,0F,b2.z+l),new Color(0F,1F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(b2.x+l,0F,b2.z-l),new Vector3(b2.x-l,0F,b2.z+l),new Color(0F,1F,1F,0.5F),0F,false);
					
			//Debug.Log("Intersection! " +Time.time);
			return false;
	
			// NOTE: previous implementation was a.d < b.d. That's simpler
			// but trouble when the segments are of dissimilar sizes. If
			// you're on a grid and the segments are similarly sized, then
			// using distance will be a simpler and faster implementation.
		}
		
	
		// Run the algorithm, sweeping over all or part of the circle to find
		// the visible area, represented as a set of triangles
		public void sweep() {
			float maxAngle = 999F;
			intersectionsOccured = false;
			//this.output = new Array<object>(new object[]{});
			//this.demo_intersectionsDetected = new Array<object>(new object[]{});
		
			output = new List<Point>();
			//demo_intersectionsDetected = new List<List<Point>>();
		
//			demo_intersectionsDetected = [];
		
			//endpoints.sort(_endpoint_compare, true);
			//endpoints.Sort( (p1, p2) => (p1.CompareTo(p2)) );//angles.Sort((p1, p2) => (p1.CompareTo(p2)));//Debug.Log("normal order");
			
			//endpoints.Sort((ep1, ep2) => (ep1.angle.CompareTo(ep2.angle)));
			endpoints.Sort((ep1, ep2) => (_endpoint_compare(ep1,ep2)));
			//endpoints.Sort((ep2, ep1) => (_endpoint_compare(ep1,ep2)));
		
			open.Clear();//open.clear(); open -> List<Segment>();
			float beginAngle = 0F;
	
			// At the beginning of the sweep we want to know which
			// segments are active. The simplest way to do this is to make
			// a pass collecting the segments, and make another pass to
			// both collect and process them. However it would be more
			// efficient to go through all the segments, figure out which
			// ones intersect the initial sweep line, and then sort them.
			for (int pass = 0; pass < 2; pass++) {
				//bool once = false;
				//int iP = 0;
			
				/*if(pass == 1){
					DrawOpen(ref open);
				}*/
			
				foreach (EndPoint p in endpoints) {
					if (pass == 1 && p.angle > maxAngle) {
						// Early exit for the visualization to show the sweep process
						break;
					}
					
					//var current_old = open.isEmpty()? null : open.head.val;
					Segment current_old = open.Count == 0? null : open[open.Count-1]; //(if) ? then : else|||head = first (last added), tail last (first added)
					
					if (p.begin) {	//begin is a Endpoint var
						// Insert into the right place in the list
						//Debug.Log(open.Count);
					
						int nodeIndex = open.Count;
						Segment node = open.Count == 0? null : open[open.Count-1];//at the beginning open is empty
						
						/*if(!once){
							Debug.DrawLine(new Vector3(p.segment.p1.x,0F,p.segment.p1.y), new Vector3(p.segment.p2.x,0F,p.segment.p2.y), Color.yellow,0F,false);
							if(node!=null)Debug.DrawLine(new Vector3(node.p1.x,0F,node.p1.y), new Vector3(node.p2.x,0F,node.p2.y), Color.red,0F,false);
							once = true;
						}*/
					
						/*for(;nodeIndex < open.Count && _segment_in_front_of(p.segment, node, center); nodeIndex++) {
							node = open[nodeIndex];
						}*/
					
						while (node != null && !_segment_in_front_of(p.segment, node, center)) {
							if(open.Count>nodeIndex){node = open[nodeIndex];}
							else{node = null;}
							nodeIndex++;
						}
										
						if (node == null) {
							open.Add(p.segment);
						}else{
							int ind = open.FindIndex(x => x == node);
							open.Insert(ind, p.segment);
					
						}					
					}else{					
						//is this an efficient removal? better remove by list iterator
						//open.remove(p.segment);
						open.Remove(p.segment);
					}
					
					Segment current_new = open.Count == 0? null : open[open.Count-1];
					if (current_old != current_new) {
						if (pass == 1) {
							addTriangle(beginAngle, p.angle, current_old);
						}
						beginAngle = p.angle;
					}
				
					//iP++;
				}
			}
		}
	
	
		public Point lineIntersection(Point p1, Point p2, Point p3, Point p4){
			// From http://paulbourke.net/geometry/lineline2d/
			var s = ((p4.x - p3.x) * (p1.y - p3.y) - (p4.y - p3.y) * (p1.x - p3.x))
				/ ((p4.y - p3.y) * (p2.x - p1.x) - (p4.x - p3.x) * (p2.y - p1.y));
			return new Point{x = p1.x + s * (p2.x - p1.x), y = p1.y + s * (p2.y - p1.y)};
		}
		
		private float maxDistance = 500F;	
		private void addTriangle(float angle1, float angle2, Segment segment) {
			Point p1 = center;
			Point p2 = new Point{x= center.x + Mathf.Cos(angle1), y=center.y + Mathf.Sin(angle1)};
			Point p3 = new Point{x=0F, y=0F};
			Point p4 = new Point{x=0F, y=0F};
	
			if (segment != null) {
				// Stop the triangle at the intersecting segment
				p3.x = segment.p1.x;
				p3.y = segment.p1.y;
				p4.x = segment.p2.x;
				p4.y = segment.p2.y;
			} else {
				// Stop the triangle at a fixed distance; this probably is
				// not what we want, but it never gets used in the demo
				p3.x = center.x + Mathf.Cos(angle1) * maxDistance;
				p3.y = center.y + Mathf.Sin(angle1) * maxDistance;
				p4.x = center.x + Mathf.Cos(angle2) * maxDistance;
				p4.y = center.y + Mathf.Sin(angle2) * maxDistance;
			}
		
			var pBegin = lineIntersection(p3, p4, p1, p2);
	
			p2.x = center.x + Mathf.Cos(angle2);
			p2.y = center.y + Mathf.Sin(angle2);
			var pEnd = lineIntersection(p3, p4, p1, p2);
	
			output.Add(pBegin);//output.push(pBegin);
			output.Add(pEnd);//output.push(pEnd);
		}
	
		private	void DrawEndpoints(ref List<EndPoint> endpoints){
			//Debug.Log(endpoints.Count);
			for(int iP = 0; iP < endpoints.Count-1; iP+=2){
				Vector3	v0 = new Vector3(endpoints[iP].x, 0F, endpoints[iP].y)		+ off;
				Vector3	v1 = new Vector3(endpoints[iP+1].x, 0F, endpoints[iP+1].y)	+ off;
				Debug.DrawLine(v0, v1, new Color(1F,0F,0F,0.5F),0F,false);
			}
		}
	
		private	void DrawOpen(ref List<Segment> open){
			//Debug.Log("OPEN:"+open.Count);
			for(int iP = 0; iP < open.Count-1; iP++){
				Vector3	v0 = new Vector3(open[iP].p1.x, 0F, open[iP].p1.y)		+ off;
				Vector3	v1 = new Vector3(open[iP].p2.x, 0F, open[iP].p2.y)		+ off;
				Debug.DrawLine(v0, v1, Color.yellow,0F,false);
			}
		}
	
		//Color[] colors = new Color[]{Color.yellow,Color.blue,Color.green};
		private	void DrawOutput(ref List<Point> output){
			//Debug.Log(output.Count);
			Vector3 off = sPos;
			for(int iP = 0; iP < output.Count-1; iP+=2){
				Vector3	v0 = new Vector3(output[iP].x, 0F, output[iP].y)		+ off;
				Vector3	v1 = new Vector3(output[iP+1].x, 0F, output[iP+1].y)	+ off;
				//Debug.DrawLine(v0, v1, colors[iP%3],0F,false);
				Debug.DrawLine(v0, v1, new Color(1F,1F,0F,0.5F),0F,false);
			}
		}
	
	
		//convert output to pass it back to GeometricVisibility.cs
		private	void ConvertOutput(ref List<Point> output){
			List<Vector3> outputV3 = new List<Vector3>();
			//we get the points in absolute coordinate system
			Vector3 off = sPos;//source as origin
			//Vector3 off = Vector3.zero;//zero as origin
			for(int iP = 0; iP < output.Count-1; iP+=2){
				outputV3.Add( new Vector3(output[iP].x, 0F, output[iP].y)		+ off);
				outputV3.Add( new Vector3(output[iP+1].x, 0F, output[iP+1].y)	+ off);
			}
			
			GetComponent<GeometricVisibility>().CreateVisibilityPolygon(ref outputV3);
		}
	
//	}
	#endregion
	
	private	void OnGUI(){
		if(!intersectionsOccured)return;
		GUI.skin.label .fontSize = 12;
		GUI.Label (new Rect(Screen.width-630,50,500,500),"Intersections Occured\nErrors may appear");
	}
}
