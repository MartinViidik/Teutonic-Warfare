using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour {

    public static bool gameEnded = false;
    public static bool levelWon = false;
    public static bool levelLost = false;
    public static bool buyingMode = true;

    public GameObject buyingUI;
    public GameObject defenseUI;
    public GameObject nodeUI;

    public MoneyBag mbag;

    void Start() {
        getComponents();
        setStages();
    }

    void Update() {
        if (!gameEnded) {
            GetLives();
            UIHandler();
            getInput();
        } else {
            Time.timeScale = 0.0F;
        }
    }

    void GetLives() {
        if (PlayerStats.lives <= 0) {
            EndGame();
            levelLost = true;
        }
    }

    void EndGame() {
        gameEnded = true;
        if (levelLost) {
            Debug.Log("Level won");
        } else {
            Debug.Log("Game over");
        }
    }

    void getComponents() {
        buyingUI = GameObject.Find("ShopCanvas");
        defenseUI = GameObject.Find("DefenseCanvas");
        nodeUI = GameObject.Find("NodeUI");
    }

    void UIHandler() {
        if (buyingMode) {
            defenseUI.SetActive(false);
            buyingUI.SetActive(true);
        } else {
            defenseUI.SetActive(true);
            buyingUI.SetActive(false);
            nodeUI.SetActive(false);
        }
    }

    void setStages() {
        gameEnded = false;
        buyingMode = true;
    }

    void getInput(){
        if (Input.GetMouseButton(0)){
            RaycastHit hit;
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit)){
                Transform objecthit = hit.transform;
                if (hit.transform.gameObject.tag == "Moneybag"){
                    Destroy(objecthit.transform.gameObject);
                }
            }
        }
    }
}
