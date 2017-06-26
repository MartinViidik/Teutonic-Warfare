using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopScript : MonoBehaviour {

    public Constructables archer;
    public Constructables crossbow;

    BuildManager buildManager;

    void Start(){
        getComponents();
    }

    public void SelectArcher(){
        Debug.Log("Archer purchased");
        buildManager.SelectBuildingToBuild(archer);
    }

    public void SelectCrossbowMan(){
        Debug.Log("Crossbowman purchased");
        buildManager.SelectBuildingToBuild(crossbow);
    }

    public void StartWave(){
        GameController.buyingMode = false;
    }

    public void getComponents(){
        buildManager = BuildManager.instance;
    }

}
