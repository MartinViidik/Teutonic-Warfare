using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildManager : MonoBehaviour {

    public static BuildManager instance;

    private Constructables buildingToBuild;
    private NodeScript SelectedNode;
    public NodeUI NodeUI;

    public GameObject buildEffect;

    public GameObject ArcherPrefab;
    public GameObject CrossbowPrefab;

    void Awake(){
        getComponents();
    }

    public bool canBuild { get { return buildingToBuild != null; } }
    public bool hasMoney { get { return PlayerStats.cash >= buildingToBuild.cost; } }

    public void SelectBuildingToBuild(Constructables Building){
        buildingToBuild = Building;
        SelectedNode = null;
    }

    public void SelectNode(NodeScript node){
        if(SelectedNode == node){
            DeselectNode();
            return;
        }
        SelectedNode = node;
        buildingToBuild = null;

        NodeUI.SetTarget(node);
        NodeUI.Show();
    }

    public void DeselectNode(){
        SelectedNode = null;
        NodeUI.Hide();
    }

    public void BuildBuildingOn(NodeScript node){

        if(PlayerStats.cash < buildingToBuild.cost){
            Debug.Log("Too poor");
            return;
        }

        PlayerStats.cash -= buildingToBuild.cost;
        PlayerStats.BuildingsPurchased++;
        Debug.Log(buildingToBuild + " built:" + " " + PlayerStats.cash + " left");

        GameObject Building = (GameObject)Instantiate(buildingToBuild.prefab, node.GetBuildPosition(), Quaternion.identity);
        node.Building = Building;

        GameObject Effect = (GameObject)Instantiate(buildEffect, node.GetBuildPosition(), Quaternion.identity);
        Destroy(Effect, 4f);
    }

    public void getComponents(){
        instance = this;
    }

}
