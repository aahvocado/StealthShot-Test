using UnityEngine;
using System.Collections;

//visibility Fader used in Raycast approach, currently FadeIn function has to be set continuously
//as soon as the visibility is rdy change this to event driven

public class VisibilityFader : MonoBehaviour {
	
	private Material thisMaterial;
	private Color color;
	
	private bool visible = false;
	private float visibilityFactor = 0F; //smooth fadeout
	private float lowA = 0F;	//lower alpha
	
	//check if color is already correct to save some calculation time for not visible objects
	private bool finishedBlack = false;
	private bool finishedWhite = false;
	
	public	void ManualInit () {
		thisMaterial = this.renderer.material;
		color = thisMaterial.color;			//get the color of the material
		thisMaterial.color = Color.black;	//apply the set alpha to the material
	}
	
	private	void Update () {
		
		//smooth color transition
		if(visible && !finishedWhite){
			visibilityFactor+=Time.deltaTime*1F;//fadeIn fast
			if(visibilityFactor>1F){
				finishedWhite = true;
			}
			finishedBlack = false;
			visibilityFactor = Mathf.Clamp(visibilityFactor,lowA,1F);
			thisMaterial.color = Color.Lerp(Color.black,color,visibilityFactor);
		}else if(!visible && !finishedBlack){
			visibilityFactor-=Time.deltaTime*0.5F;//darken slowly
			if(visibilityFactor<lowA){
				finishedBlack = true;
			}
			finishedWhite = false;
			visibilityFactor = Mathf.Clamp(visibilityFactor,lowA,1F);
			thisMaterial.color = Color.Lerp(Color.black,color,visibilityFactor);	
		}visible = false;
	}
	
	public	void FadeIn(){
		visible = true;
	}
	
	//make sure manual init has been called already
	public	void SetBlackness(float blackness){
		lowA = blackness;
		thisMaterial.color = Color.Lerp(Color.black,color,blackness);
		finishedWhite = false;
		finishedBlack = false;
	}
}