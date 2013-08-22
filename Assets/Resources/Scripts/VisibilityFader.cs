using UnityEngine;
using System.Collections;

//visibility Fader used in Raycast approach, currently FadeIn function has to be set continuously
//as soon as the visibility is rdy change this to event driven

public class VisibilityFader : MonoBehaviour {
	
	private	Material thisMaterial;
	private	Color origColor;
	
	private	bool visible = false;
	private	float visibilityFactor = 0F; //smooth fadeout
	private	float lowA = 0F;	//lower alpha
	private	float higA = 0.85F;	//lower alpha
	
	//check if color is already correct to save some calculation time for not visible objects
	private	bool finishedBlack = false;
	private	bool finishedWhite = false;
	
	private	Color fadeColor = Color.black;
	
	public	void ManualInit () {
		thisMaterial = this.renderer.material;
		origColor = thisMaterial.color;			//get the color of the material
		thisMaterial.color = Color.black;	//apply the set alpha to the material
		
		//renderer.material.GetColor("_TintColor") for particle shaders test with:
		//if (renderer.material.HasProperty("_Color"))
		//	renderer.material.SetColor("_Color", Color.red);
		//}
	}
	
	private	void Update () {
		
		//smooth color transition
		if(visible && !finishedWhite){
			visibilityFactor+=Time.deltaTime*4F;//fadeIn fast
			if(visibilityFactor>higA){
				finishedWhite = true;
			}
			finishedBlack = false;
			visibilityFactor = Mathf.Clamp(visibilityFactor,lowA,higA);
			thisMaterial.color = Color.Lerp(fadeColor,origColor,visibilityFactor);
		}else if(!visible && !finishedBlack){
			visibilityFactor-=Time.deltaTime*1F;//darken slowly
			if(visibilityFactor<lowA){
				finishedBlack = true;
			}
			finishedWhite = false;
			visibilityFactor = Mathf.Clamp(visibilityFactor,lowA,higA);
			thisMaterial.color = Color.Lerp(fadeColor,origColor,visibilityFactor);	
		}//visible = false;
	}
	
	public	void Fade(bool visible){
		this.visible = visible;
	}
	
	//make sure manual init has been called already
	public	void SetBlackness(float blackness){
		lowA = blackness;
		thisMaterial.color = Color.Lerp(Color.black,origColor,blackness);
		finishedWhite = false;
		finishedBlack = false;
	}
	
	public	void EnableTransparentFade(bool transparent){
		if(transparent){
			//shader with transparency needed on object
			fadeColor = Color.clear;
		}else{
			fadeColor = Color.black;
		}
	}
	
}