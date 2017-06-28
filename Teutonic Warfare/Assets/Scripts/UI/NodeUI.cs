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

    public void Rotate(){
        node.transform.Rotate(0, 90, 0);
        Debug.Log("Node rotated");
    }

    public void Sell(){
        Debug.Log("Building sold");
    }

    public void Hide(){
        ui.SetActive(false);
    }

    public void Show(){
        ui.SetActive(true);
    }

}
