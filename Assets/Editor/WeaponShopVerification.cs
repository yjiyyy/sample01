using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class WeaponShopVerification
{
    private const string Key="WeaponShopVerification";
    private static IEnumerator routine;
    private static double next;
    private static int checks;
    public static void Preview()
    {
        SessionState.SetBool(Key+"Preview",true);
        Run();
    }
    public static void Run()
    {
        Directory.CreateDirectory("Logs/WeaponShop"); Directory.CreateDirectory("Logs/DevCheats");
        SetupLobbyShopPanel.CreateOrUpdateCatalog();
        EditorSceneManager.OpenScene("Assets/Scenes/03_Lobby.unity");
        SessionState.SetBool(Key,true); SessionState.SetInt(Key+"Result",0); SessionState.SetInt(Key+"Round",0);
        EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] private static void Subscribe()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                routine=Verify(); next=EditorApplication.timeSinceStartup+2; EditorApplication.update+=Tick;
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                if(SessionState.GetInt(Key+"Round",0)==0 && SessionState.GetInt(Key+"Result",0)==0)
                {
                    SessionState.SetInt(Key+"Round",1); EditorApplication.delayCall+=()=>EditorApplication.EnterPlaymode(); return;
                }
                SessionState.SetBool(Key,false); EditorApplication.Exit(SessionState.GetInt(Key+"Result",1));
            }
        };
    }
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;
        try { if(routine.MoveNext()){next=EditorApplication.timeSinceStartup+Convert.ToDouble(routine.Current);return;} }
        catch(Exception e){Debug.LogException(e);SessionState.SetInt(Key+"Result",1);}
        EditorApplication.update-=Tick; EditorApplication.ExitPlaymode();
    }
    private static IEnumerator Verify()
    {
        if(SessionState.GetBool(Key+"Preview",false))
        {
            AccountSession.Add(ShopCurrency.Money,9950);
            LobbyShopPanel.EnsureOnLobbyCanvas().Show(); yield return 1;
            Capture("shop-final.png");
            SessionState.SetBool(Key+"Preview",false); SessionState.SetInt(Key+"Round",1);
            yield break;
        }
        Check(AccountInventory.Weapons.Count==0,"New Play clears inventory");
        var equipment=GameObject.Find("CharacterSpawnPoint").GetComponentInChildren<PlayerEquipmentController>();
        if(SessionState.GetInt(Key+"Round",0)==1)
        {
            Check(equipment.IsUnarmed(equipment.GetSlot(0)),"New Play clears purchased loadout");
            File.AppendAllText("Logs/WeaponShop/result.txt","\nRestart checks passed"); yield break;
        }
        var catalog=LobbyShopCatalogSO.Load();
        Check(catalog.Categories.Length==2 && catalog.GetCategory(1).items.Length==0,"Two categories, Other empty");
        Check(catalog.GetCategory(0).items.Length>=11,"Existing player weapons registered");
        var character=AccountSession.SelectedCharacter;
        var panel=LobbyShopPanel.EnsureOnLobbyCanvas(); panel.Show();
        yield return 1;
        var item=panel.SelectedItem;
        var original=equipment.GetSlot(equipment.ActiveSlotIndex);
        Check(equipment.CurrentWeaponData==item.weapon,"Selection previews actual weapon");
        Check(equipment.GetSlot(equipment.ActiveSlotIndex)==original && !AccountInventory.Owns(item.weapon),"Preview changes neither slot nor ownership");
        panel.Hide(); Check(equipment.CurrentWeaponData==original,"Closing restores actual weapon");
        panel.Show();
        Check(!Find<Button>(panel,"BuyButton").interactable,"Insufficient funds disables purchase");
        AccountSession.Add(ShopCurrency.Money,100000); AccountSession.Add(ShopCurrency.Gem,500);
        yield return .5;
        Capture("shop-before-purchase.png");
        typeof(DevCheatVerification).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{1280,720,"shop-720p.png"});
        File.Copy("Logs/DevCheats/shop-720p.png","Logs/WeaponShop/shop-720p.png",true);
        int before=AccountSession.Money;
        Find<Button>(panel,"BuyButton").onClick.Invoke();
        Check(AccountInventory.Owns(item.weapon) && AccountInventory.Weapons.Count==1,"Purchase creates inventory item");
        Check(AccountSession.Money==before-item.price,"Purchase charges exactly once");
        Check(equipment.GetSlot(equipment.ActiveSlotIndex)==item.weapon,"Purchase automatically equips");
        Check(!AccountInventory.TryBuy(item),"Duplicate purchase refused");
        panel.Hide();Check(equipment.CurrentWeaponData==item.weapon,"Closing preserves purchased equip");
        panel.ShowInventory(); Check(panel.SelectedItem.weapon==item.weapon,"Inventory displays purchased weapon");
        panel.SwitchSlot();Check(!panel.CanEquip(item.weapon),"Same weapon cannot occupy both slots");
        panel.SwitchSlot();panel.Hide();
        panel.Show();panel.SelectCategory(1);Check(panel.SelectedItem==null && !Find<Button>(panel,"BuyButton").interactable,"Empty Other has no stale selection");
        panel.SelectCategory(0);
        var other=catalog.GetCategory(0).items[1];
        var stats=equipment.GetComponent<PlayerStats>(); float strength=stats.strength;
        stats.strength=0;panel.SelectItem(1);
        Check(equipment.CurrentWeaponData==other.weapon && !panel.CanEquip(other.weapon),"Heavy weapon preview ignores equip limit");
        panel.PurchaseSelected();
        Check(AccountInventory.Owns(other.weapon) && equipment.GetSlot(equipment.ActiveSlotIndex)==item.weapon,"Insufficient strength stores item without equipping");
        stats.strength=strength;
        panel.Hide();panel.Show();
        var scroll=panel.GetComponentInChildren<ScrollRect>(); Canvas.ForceUpdateCanvases();
        Check(scroll.content.rect.height>scroll.viewport.rect.height,"Long list scrolls");
        scroll.verticalNormalizedPosition=0;yield return .5;
        Check(scroll.verticalNormalizedPosition<.01f,"Last row reachable");
        Capture("shop-scroll-bottom.png");
        panel.Hide();
        SceneManager.LoadScene(SceneNames.CharacterSelection);yield return 2;
        Check(AccountInventory.Weapons.Count==2,"Inventory survives character selection scene");
        var second=LobbyStatUpgradeCatalogSO.Load().characters.First(p=>p.character!=character).character;
        AccountSession.SelectedCharacter=second;SceneManager.LoadScene(SceneNames.Lobby);yield return 2;
        equipment=GameObject.Find("CharacterSpawnPoint").GetComponentInChildren<PlayerEquipmentController>();
        Check(AccountInventory.Owns(item.weapon) && equipment.IsUnarmed(equipment.GetSlot(0)),"Ownership shared; second character loadout independent");
        AccountSession.SelectedCharacter=character;SceneManager.LoadScene("Stage00");yield return 3;
        Check(SceneManager.GetActiveScene().name=="Stage00","Battle loaded");
        equipment=PlayerResources.Instance.GetComponent<PlayerEquipmentController>();
        Check(equipment!=null && equipment.GetSlot(0)==item.weapon,"Purchased loadout reaches battle");
        SceneManager.LoadScene(SceneNames.Lobby);yield return 2;
        panel=LobbyShopPanel.EnsureOnLobbyCanvas();panel.ShowInventory();yield return .5;
        Capture("inventory.png");
        panel.Hide();
        foreach(var offer in catalog.GetCategory(0).items)
        {
            if(AccountInventory.Weapons.Count>=AccountInventory.Capacity)break;
            if(!AccountInventory.Owns(offer.weapon))Check(AccountInventory.TryBuy(offer),"Fill inventory");
        }
        Check(AccountInventory.Weapons.Count==10,"Default capacity is ten");
        var remaining=catalog.GetCategory(0).items.First(i=>!AccountInventory.Owns(i.weapon));
        before=AccountSession.Money;
        Check(!AccountInventory.TryBuy(remaining) && AccountSession.Money==before,"Full inventory rejects purchase without charge");
        panel.Show();panel.SelectItem(Array.IndexOf(catalog.GetCategory(0).items,remaining));
        Check(!Find<Button>(panel,"BuyButton").interactable,"Full inventory disables button");
        Capture("inventory-full.png");
        File.WriteAllText("Logs/WeaponShop/result.txt",checks+" checks passed");
    }
    private static T Find<T>(Component root,string name) where T:Component => root.GetComponentsInChildren<T>(true).First(c=>c.name==name && c.gameObject.activeInHierarchy);
    private static void Capture(string name)
    {
        typeof(DevCheatVerification).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{1920,1080,name});
        File.Copy("Logs/DevCheats/"+name,"Logs/WeaponShop/"+name,true);
    }
    private static void Check(bool value,string message)
    {
        if(!value)throw new Exception("Shop check failed: "+message);
        checks++;Debug.Log("[WeaponShopVerification] PASS "+message);
    }
}
