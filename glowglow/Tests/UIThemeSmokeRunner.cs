// Copy only into the isolated validation project's Assets folder.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GlowGlow.Online;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class UIThemeSmokeRunner : MonoBehaviour
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly List<string> Results=new();
    private static int checks;
    private static bool booted;
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if(booted || !Application.isBatchMode || !Environment.CommandLine.Contains("UIThemeSmokeRunner.Begin")) return;
        booted=true;
        var runner=new GameObject("UI theme validation").AddComponent<UIThemeSmokeRunner>();
        DontDestroyOnLoad(runner); runner.StartCoroutine(Run());
    }
    private static IEnumerator Run()
    {
        Directory.CreateDirectory("ValidationOutput");
        var iterator=Verify();
        while(true)
        {
            object next;
            try { if(!iterator.MoveNext()) break; next=iterator.Current; }
            catch(Exception ex)
            {
                Time.timeScale=1;
                File.WriteAllText("ValidationOutput/ui-theme-result.txt",string.Join("\n",Results)+"\nFAIL: "+ex);
                Debug.LogException(ex); EditorApplication.Exit(1); yield break;
            }
            yield return next;
        }
        Results.Add("PASS: "+checks+" UI theme checks.");
        File.WriteAllText("ValidationOutput/ui-theme-result.txt",string.Join("\n",Results));
        Debug.Log("UI_THEME_PASSED: "+checks); EditorApplication.Exit(0);
    }
    private static IEnumerator Verify()
    {
        QualitySettings.vSyncCount=0; Application.targetFrameRate=120;
        yield return new WaitForSecondsRealtime(.5f);
        var atlas=Resources.LoadAll<Sprite>("UI/NeonUIAtlas");
        Check(atlas.Length==32,"32 prepared UI sprites load from one atlas");
        Check(atlas.Select(s=>s.texture).Distinct().Count()==1,"Every frame and glyph shares one texture");
        var texture=atlas[0].texture;
        Check(texture.width==1024 && texture.height==512 && texture.mipmapCount==1 && !texture.isReadable,"Atlas is 2 MiB of RGBA32 texels, without mipmaps or CPU readability");
        Check(NeonUI.Asset("Panel Frame").border==new Vector4(28,28,28,28),"Panels use prepared nine-slice borders");
        Check(atlas.All(s=>s.packed==false),"No runtime sprite packing is required");
        var title=FindFirstObjectByType<TitleScreenController>();
        Check(title!=null,"Real title scene loads");
        var menu=(CanvasGroup)Field(title,"mainMenu");
        Check(menu.GetComponentsInChildren<Image>().All(i=>i.sprite==null || i.sprite.texture!=texture),"Main menu artwork remains unchanged");
        Capture("ui-main.png",1600,900);

        title.PlaySolo(); yield return new WaitForSecondsRealtime(.3f);
        var solo=(CanvasGroup)Field(title,"soloPanel");
        Check(solo.gameObject.activeSelf && solo.GetComponent<Image>().sprite==NeonUI.Asset("Panel Fill"),"Singleplayer uses the common panel skin");
        var scroll=solo.GetComponentInChildren<ScrollRect>();
        Check(scroll!=null && !scroll.horizontal && scroll.viewport.GetComponent<RectMask2D>()!=null,"Mode list keeps scroll and clipping behavior");
        Capture("ui-singleplayer.png",1600,900);
        var bot=solo.GetComponentsInChildren<Button>().Single(b=>b.name=="봇 대전");
        bot.onClick.Invoke(); yield return null;
        var setup=FindFirstObjectByType<BotMatchSetup>();
        var input=(TMP_InputField)Field(setup,"ratingInput");
        input.text="9999";
        Check(!((Button)Field(setup,"startButton")).interactable,"Bot setup retains rating validation");
        input.text="2400";
        Check(((Button)Field(setup,"startButton")).interactable && ((Slider)Field(setup,"ratingSlider")).value==2400,"Bot input and slider stay synchronized");
        Capture("ui-bot-setup.png",1600,900);
        Call(setup,"Close"); yield return null;
        ((Button)Field(title,"soloBackButton")).onClick.Invoke(); yield return new WaitForSecondsRealtime(.3f);

        title.OpenSettings(); yield return new WaitForSecondsRealtime(.3f);
        var settings=(CanvasGroup)Field(title,"settingsPanel");
        var volume=(Slider)Field(title,"masterVolume");
        Check(volume.handleRect.GetComponent<Image>().sprite==NeonUI.Asset("Diamond"),"Settings slider uses the shared diamond handle");
        Check(((Slider)Field(title,"barrageOpacity")).navigation.selectOnUp==volume,"Settings keyboard navigation retained");
        Capture("ui-settings.png",1600,900);
        title.CloseSettings(); yield return new WaitForSecondsRealtime(.3f);

        title.OpenDeck(); yield return new WaitForSecondsRealtime(.3f);
        var deck=FindFirstObjectByType<DeckScreen>();
        Check(deck.HasPreview && deck.DeckSlotCapacity==8,"Deck keeps live preview and eight slots");
        var cells=(List<Button>)Field(deck,"collectionSlots");
        cells[1].onClick.Invoke();
        Check(cells[1].targetGraphic.GetComponent<Image>().sprite==NeonUI.Asset("Selected Frame"),"Selecting a barrage updates its luminous frame");
        for(int page=0;page<4;page++)
        {
            Capture("ui-deck-"+(page+1)+".png",1600,900);
            if(page<3) ((Button)Field(deck,"next")).onClick.Invoke();
        }
        Check(cells.Count(c=>c.gameObject.activeSelf)==1,"Last collection page still contains the nineteenth barrage");
        deck.BackButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(.3f);
        title.PlayMultiplayer(); yield return new WaitForSecondsRealtime(.3f);
        Capture("ui-multiplayer.png",1600,900);
        OnlineSession.Open(AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset"));
        yield return new WaitForSecondsRealtime(.2f);
        Check(FindFirstObjectByType<OnlineScreen>()!=null,"Online lobby skin initializes without creating a connection");
        Capture("ui-online.png",1600,900);
        OnlineSession.Current.Back(); yield return new WaitForSecondsRealtime(.4f);

        MatchController.RequestBotMatch(1200); SceneManager.LoadScene("Arena");
        yield return new WaitForSecondsRealtime(.5f);
        var match=FindFirstObjectByType<MatchController>();
        Check(match.IsDrafting && !match.IsPlaying,"UI restyle preserves draft gating");
        var draft=FindFirstObjectByType<OpeningDraftScreen>();
        yield return new WaitForSecondsRealtime(1.15f);
        Call(draft,"Toggle",1);
        yield return new WaitForSecondsRealtime(.15f);
        Capture("ui-draft.png",1600,900);
        Check(((Button)Field(draft,"confirm")).interactable,"Draft selection enables confirm");
        Check(((Button)Field(draft,"confirm")).targetGraphic.canvasRenderer.GetColor().a>.8f,"Confirm frame finishes its focus transition");
        for(int round=0;round<3;round++)
        {
            if(round>0) { yield return new WaitForSecondsRealtime(1.1f); Call(draft,"Toggle",0); }
            Call(draft,"FinishRound",false);
        }
        Check(match.IsPlaying && !match.IsDrafting,"All three draft confirmations still begin combat");
        yield return new WaitForSecondsRealtime(.15f);
        match.PlayerOne.SetInputSource(new Idle()); match.PlayerTwo.SetInputSource(new Idle());
        ProjectileBase.DespawnAll();
        var bar=FindFirstObjectByType<DrawnWeaponBar>();
        Capture("ui-combat.png",1600,900);
        for(int i=0;i<100;i++) bar.Refresh();
        long start=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<100;i++) bar.Refresh();
        long allocated=GC.GetAllocatedBytesForCurrentThread()-start;
        Check(allocated==0,"Warmed combat slot refresh allocates zero bytes across 100 calls");
        typeof(PlayerCombatant).GetField("invulnerableUntil",Flags).SetValue(match.PlayerTwo,Time.time-1);
        match.PlayerTwo.ReceiveHit(match.PlayerOne,100);
        yield return null;
        var result=GameObject.Find("Match Result Card");
        Check(!match.IsPlaying && result!=null && result.GetComponentsInChildren<Button>().Length==2,"Result card exposes working rematch and exit controls");
        Capture("ui-result.png",1600,900);

        MatchController.RequestTraining(); SceneManager.LoadScene("Arena");
        yield return new WaitForSecondsRealtime(.5f);
        var training=FindFirstObjectByType<TrainingGround>();
        Check(training!=null,"Training screen initializes");
        training.SelectWeapon(0);
        Capture("ui-training.png",1600,900);
        Capture("ui-training-compact.png",1200,900);
        Capture("ui-training-wide.png",1920,1080);
        Check(FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b=>b.name=="Basic Shot").Any(b=>b.targetGraphic.GetComponent<Image>().sprite==NeonUI.Asset("Selected Frame")),"Training shows the selected barrage frame");
        Check(FindObjectsByType<Image>(FindObjectsSortMode.None).Where(i=>i.name=="Icon" && i.sprite==training.Selected.Definition.icon).All(i=>i.color==Color.white),"Prepared barrage icons retain original colors");
        var selected=training.Selected;
        Call(training,"SetAll",true);
        Check(selected.Upgrades.Count==3,"Training upgrade controls still apply all three upgrades");
        FindFirstObjectByType<MatchController>().PlayerTwo.ReceiveHit(FindFirstObjectByType<MatchController>().PlayerOne,6);
        yield return null;
        Capture("ui-training-upgraded.png",1600,900);
        training.ResetArena();
        Check(FindFirstObjectByType<MatchController>().PlayerTwo.Health==100,"Training reset keeps health behavior");
    }
    private sealed class Idle:IPlayerInputSource { public PlayerCommand ReadCommand(Vector2 p)=>new(){SelectedSlot=-1,Aim=Vector2.right,AimPosition=p+Vector2.right*4}; }
    private static object Field(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
    private static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
    private static void Check(bool value,string message) { if(!value) throw new Exception(message); checks++; Results.Add("PASS: "+message); }
    private static void Capture(string file,int width,int height)
    {
        var camera=Camera.main;
        var output=new RenderTexture(width,height,24);
        var previousTarget=camera.targetTexture; var previousActive=RenderTexture.active;
        var canvases=FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas && c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        camera.targetTexture=output;
        foreach(var canvas in canvases) { canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1; }
        camera.GetComponent<ArenaCameraLayout>()?.Apply();
        Canvas.ForceUpdateCanvases();
        var training=FindFirstObjectByType<TrainingGround>(); if(training!=null) Call(training,"LateUpdate");
        Canvas.ForceUpdateCanvases();
        int overflow=0;
        foreach(var label in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
        {
            if(label.GetComponentInParent<CanvasGroup>() is CanvasGroup group && group.alpha<.1f) continue;
            label.ForceMeshUpdate();
            if(label.isTextOverflowing) { Results.Add("OVERFLOW "+file+": "+label.name+" / "+label.text.Replace('\n',' ')); overflow++; }
        }
        Results.Add("CAPTURE "+file+": "+width+"x"+height+"; overflowing text fields="+overflow);
        camera.Render(); RenderTexture.active=output;
        var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
        pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
        File.WriteAllBytes("ValidationOutput/"+file,pixels.EncodeToPNG());
        RenderTexture.active=previousActive; camera.targetTexture=previousTarget;
        foreach(var canvas in canvases) { canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null; }
        Destroy(pixels); output.Release(); Destroy(output);
    }
}
