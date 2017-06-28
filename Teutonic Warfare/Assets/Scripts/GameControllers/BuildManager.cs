using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildManager : MonoBehaviour {

    public static BuildManager instance;

    private Constructables buildingToBuild;
    private NodeScript SelectedNode;
    public NodeUI NodeUI;

    public GameObject buildEffect;
    public GameObject buildNodes;

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

    public Constructables GetBuildingToBuild(){
        return buildingToBuild;
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

    public void getComponents(){
        instance = this;
    }

}
