using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour {

    public static bool gameEnded = false;
    public static bool buyingMode = true;

    public GameObject buyingUI;
    public GameObject defenseUI;

	void Start () {
        getComponents();
        setStages();
	}
	
	void Update () {
        if(!gameEnded){
            GetLives();
            UIHandler();
        }
	}

    void GetLives(){
        if(PlayerStats.lives <= 0){
            EndGame();
        }
    }

    void EndGame(){
        gameEnded = true;
        Debug.Log("Game over");
    }

    void getComponents(){
        buyingUI = GameObject.Find("ShopCanvas");
        defenseUI = GameObject.Find("DefenseCanvas");
    }

    void UIHandler(){
        if (buyingMode){
            defenseUI.SetActive(false);
            buyingUI.SetActive(true);
        } else {
            defenseUI.SetActive(true);
            buyingUI.SetActive(false);
        }
    }

    void setStages(){
        gameEnded = false;
        buyingMode = true;
    }
}
