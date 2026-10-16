using System;
using System.Collections.Generic;
using System.Linq;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    // Local art/input fixtures. Resource injection here is never a natural-run claim.
    public static class LabArtAcceptance
    {
        static OperationScreenView Op => Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include);
        static OperationPayloadDrag _source;
        static PointerEventData _gesture;
        static string _carriedId;
        static void Need(bool condition, string text) { if (!condition) throw new InvalidOperationException("LAB_FAIL: " + text); }
        static Vector2 Center(Transform transform)
        {
            var rect = (RectTransform)transform;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }
        static void Hit(Transform transform)
        {
            Canvas.ForceUpdateCanvases();
            var data = new PointerEventData(EventSystem.current) { position = Center(transform) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data,hits);
            Need(hits.Count > 0 && (hits[0].gameObject.transform == transform || hits[0].gameObject.transform.IsChildOf(transform)), "Raycast blocked: " + transform.name);
        }
        public static string CheckLayout()
        {
            var op = Op;
            var plans = TheCallApp.Interface.SendQuery(new BreedingPlansQuery());
            Need(op.lab.room2Lock.activeSelf == (plans.Count == 1),"Second room lock differs from rules");
            var cursor=0;
            for(var room=0;room<plans.Count;room++)
                for(var parent=0;parent<plans[room].ParentIds.Count;parent++,cursor++)
                {
                    var slot=op.breedingSlots[cursor];
                    Need(slot.index == cursor && slot.gameObject.activeInHierarchy,"Parent slot identity/visibility " + cursor);
                    var x=((RectTransform)slot.transform).anchoredPosition.x;
                    Need(room==0 ? x<700 : x>800,"Parent assigned to wrong painted room " + cursor);
                    Hit(slot.transform);
                }
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Need(pipeline!=null && pipeline.GetRenderer(0).GetType().Name == "Renderer2D","Actual runtime renderer is not Renderer2D");
            Need(RenderPipelineManager.currentPipeline is UniversalRenderPipeline,"URP not running");
            return "PASS runtime Renderer2D, painted room locks, " + cursor + " mapped parents and visible raycast targets";
        }
        public static string SelectResearch(string name)
        {
            var research=Object.FindAnyObjectByType<ResearchScreenView>();
            var node=research.nodes.Single(node=>node.techName==name);
            HandbookAcceptance.ClickVisible(node.name);
            return "PASS actual research selection " + name;
        }
        public static string CheckUnlocked(string name)
        {
            Need(TheCallApp.Interface.SendQuery(new RunLedgerQuery()).UnlockedTech.Contains(name),"Research failed " + name);
            return "PASS actual research clicks unlock " + name;
        }
        public static string CheckResearchText()
        {
            var research=Object.FindAnyObjectByType<ResearchScreenView>();
            foreach(var node in research.nodes)
            {
                var text=node.transform.Find("LabSummary").GetComponent<TMP_Text>();
                text.ForceMeshUpdate();
                Need(text.textBounds.size.x <= text.rectTransform.rect.width+8,"Research summary overflows its card: " + node.techName);
            }
            return "PASS all six research summaries stay inside their rendered card bounds";
        }
        public static string BeginCarry()
        {
            Op.lab.cageScroll.verticalNormalizedPosition=1;
            Canvas.ForceUpdateCanvases();
            var slot=Op.cageSlots.First(slot=>slot.gameObject.activeInHierarchy && !string.IsNullOrEmpty(slot.monsterId));
            Hit(slot.transform);
            _carriedId=slot.monsterId; _source=slot.GetComponent<OperationPayloadDrag>();
            _gesture=new PointerEventData(EventSystem.current) {position=Center(slot.transform),pressPosition=Center(slot.transform),button=PointerEventData.InputButton.Left};
            _source.OnPointerDown(_gesture); _source.OnBeginDrag(_gesture);
            _gesture.position=Center(Op.extractionSlots[0].transform); _source.OnDrag(_gesture);
            var ghost=Op.dragLayer.Find("MonsterGhost");
            Need(ghost!=null && ghost.localScale.x>1.4f,"Small cage ghost did not enlarge near production");
            Need(Mathf.Abs(CanvasMap.ScreenUniform((RectTransform)ghost)-CanvasMap.ScreenUniform((RectTransform)Op.extractionSlots[0].portrait.transform))<1,"Drag ghost does not match the destination screen viewport");
            return "PASS actual drag ghost scales to production: " + ghost.localScale.x.ToString("F2");
        }
        public static string FinishCarry()
        {
            Need(_source!=null,"Begin a carry first");
            _source.OnEndDrag(_gesture);
            Need(Op.extractionSlots[0].monsterId==_carriedId,"Carry did not commit through rule command");
            _source=null;
            return "PASS enlarged ghost commits to extraction through actual raycast drop";
        }
        public static string PlaceFirstParent()
        {
            Op.lab.cageScroll.verticalNormalizedPosition=1; Canvas.ForceUpdateCanvases();
            var slot=Op.cageSlots.First(slot=>slot.gameObject.activeInHierarchy && !string.IsNullOrEmpty(slot.monsterId));
            Hit(slot.transform); Hit(Op.breedingSlots[0].transform);
            var id=slot.monsterId;
            var source=slot.GetComponent<OperationPayloadDrag>();
            var data=new PointerEventData(EventSystem.current){position=Center(slot.transform),pressPosition=Center(slot.transform),button=PointerEventData.InputButton.Left};
            source.OnPointerDown(data); source.OnBeginDrag(data); data.position=Center(Op.breedingSlots[0].transform); source.OnDrag(data); source.OnEndDrag(data);
            Need(Op.breedingSlots[0].monsterId==id,"Drop did not reach first parent");
            return "PASS real cage to painted parent-room drop";
        }
        public static string HoverRecycler(bool entered)
        {
            Hit(Op.discardButton.transform);
            var data=new PointerEventData(EventSystem.current){position=Center(Op.discardButton.transform)};
            if(entered) ExecuteEvents.Execute(Op.discardButton.gameObject,data,ExecuteEvents.pointerEnterHandler);
            else ExecuteEvents.Execute(Op.discardButton.gameObject,data,ExecuteEvents.pointerExitHandler);
            return "PASS recycler hover " + entered;
        }
        public static string CheckMouth(bool open)
        {
            Need(open ? Op.lab.recycler.Openness>0.95f : Op.lab.recycler.Openness<0.05f,"Recycler jaw did not reach target pose");
            return "PASS animated recycler is " + (open?"open":"closed");
        }
        public static string Discard()
        {
            Op.lab.cageScroll.verticalNormalizedPosition=1; Canvas.ForceUpdateCanvases();
            var slot=Op.cageSlots.First(slot=>slot.gameObject.activeInHierarchy && !string.IsNullOrEmpty(slot.monsterId));
            Hit(slot.transform);
            var id=slot.monsterId;
            var count=Op.lab.recycler.ConsumedCount;
            var source=slot.GetComponent<OperationPayloadDrag>();
            var data=new PointerEventData(EventSystem.current){position=Center(slot.transform),pressPosition=Center(slot.transform),button=PointerEventData.InputButton.Left};
            source.OnPointerDown(data); source.OnBeginDrag(data);
            data.position=Center(Op.discardButton.transform); source.OnDrag(data); source.OnEndDrag(data);
            Need(TheCallApp.Interface.SendQuery(new MonsterQuery(id))==null,"Monster not discarded by rules");
            Need(TheCallApp.Interface.SendQuery(new GeneSlotsQuery()).Any(gene=>!string.IsNullOrEmpty(gene.SkillName)),"Discard produced no gene");
            Need(Op.lab.recycler.ConsumedCount==count+1,"Successful discard did not trigger jaw animation");
            return "PASS raycast recycle removes monster, yields gene and triggers one chew";
        }
        public static string CheckEnergy()
        {
            Need(Op.lab.EnergyArrivals>0,"Scoring never notified the painted collector");
            return "PASS real settlement reached collector " + Op.lab.EnergyArrivals + " times";
        }
        public static string Reset()
        {
            Object.FindAnyObjectByType<TheCallPresentation>().Perform(PresentationAction.ReturnToTitle);
            Need(!TheCall.Scoring.ScoringShow.IsPlaying,"Reset left live scoring");
            Need(Op.lab.motes.All(mote=>!mote.gameObject.activeSelf),"Reset left live energy motes");
            Need(Op.lab.recycler.Openness==0,"Reset left recycler open");
            return "PASS reset retires scoring, energy motes and mouth motion";
        }
    }
}
