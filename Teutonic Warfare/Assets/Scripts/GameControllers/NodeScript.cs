using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class NodeScript : MonoBehaviour {

    public Color tapColor;
    public Color poorColor;
    public Vector3 positionOffset;

    public GameObject Building;

    private Renderer rend;
    private Color startColor;

    BuildManager buildManager;

    void Start(){
        getComponents();
    }

    public Vector3 GetBuildPosition(){
        return transform.position + positionOffset;
    }

    void OnMouseDown(){
        if (EventSystem.current.IsPointerOverGameObject())
            return;
        if(Building != null){
            Debug.Log("Node occupied");
            buildManager.SelectNode(this);
            return;
        }
        if (!buildManager.canBuild)
            return;

        buildManager.BuildBuildingOn(this);
        buildManager.SelectBuildingToBuild(null);

    }

    void OnMouseEnter(){
        if (EventSystem.current.IsPointerOverGameObject())
            return;
        if (!buildManager.canBuild)
            return;
        if (buildManager.hasMoney){
            rend.material.color = tapColor;
        } else {
            rend.material.color = poorColor;
        }
    }

    void OnMouseExit(){
        rend.material.color = startColor;
    }

    void getComponents(){
        rend = GetComponent<Renderer>();
        startColor = rend.material.color;

        buildManager = BuildManager.instance;
    }

}
