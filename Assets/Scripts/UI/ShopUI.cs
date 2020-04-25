using UnityEngine;
using TMPro;

public class ShopUI : MonoBehaviour {

    public TMP_Text CashText;
	
	void Update () {
        DisplayCash();
	}

    void DisplayCash(){
        CashText.text = PlayerStats.cash.ToString();
    }
}
