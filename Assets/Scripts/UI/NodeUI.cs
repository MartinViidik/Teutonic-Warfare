using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NodeUI : MonoBehaviour {

    private NodeScript node;
    public GameObject ui;

    void Start(){
        Hide();
    }

    public void SetTarget(NodeScript target){
        node = target;
        transform.position = node.GetBuildPosition();
    }

    public void Upgrade(){
        node.UpgradeBuilding();
        BuildManager.instance.DeselectNode();
    }
    public void Sell(){
        node.SellBuilding();
        BuildManager.instance.DeselectNode();
    }

    public void Rotate(){
        node.RotateBuilding();
    }

    public void Hide(){
        ui.SetActive(false);
    }

    public void Show(){
        ui.SetActive(true);
    }

}
