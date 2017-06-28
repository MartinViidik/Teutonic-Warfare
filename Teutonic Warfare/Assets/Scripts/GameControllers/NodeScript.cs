using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class NodeScript : MonoBehaviour {

    public Color tapColor;
    public Color poorColor;
    public Vector3 positionOffset;

    public GameObject Building;
    public Constructables BuildingBlueprint;
    public bool isUpgraded = false;

    public Renderer rend;
    private Color startColor;

    BuildManager buildManager;

    void Start(){
        getComponents();
    }

    void Update(){

    }

    public Vector3 GetBuildPosition(){
        return transform.position + positionOffset;
    }

    void BuildBuilding(Constructables blueprint){
        if (PlayerStats.cash < blueprint.cost){
            Debug.Log("Too poor");
            return;
        }

        PlayerStats.cash -= blueprint.cost;
        PlayerStats.BuildingsPurchased++;
        Debug.Log(blueprint + " built:" + " " + PlayerStats.cash + " left");

        GameObject _building = (GameObject)Instantiate(blueprint.prefab, GetBuildPosition(), Quaternion.identity);
        Building = _building;
        BuildingBlueprint = blueprint;

        GameObject Effect = (GameObject)Instantiate(buildManager.buildEffect, GetBuildPosition(), Quaternion.identity);
        buildManager.SelectBuildingToBuild(null);
        Destroy(Effect, 4f);
    }

    public void UpgradeBuilding(){
        if (PlayerStats.cash < BuildingBlueprint.upgradeCost){
            Debug.Log("Too poor");
            return;
        }

        PlayerStats.cash -= BuildingBlueprint.upgradeCost;
        PlayerStats.BuildingsPurchased++;
        Debug.Log(BuildingBlueprint + " upgraded:" + " " + PlayerStats.cash + " left");

        Destroy(Building);
        GameObject _building = (GameObject)Instantiate(BuildingBlueprint.upgradedPrefab, GetBuildPosition(), Quaternion.identity);
        Building = _building;
        isUpgraded = true;

        GameObject Effect = (GameObject)Instantiate(buildManager.buildEffect, GetBuildPosition(), Quaternion.identity);
        Destroy(Effect, 4f);
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

        BuildBuilding(buildManager.GetBuildingToBuild());
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
