﻿module MAG.Server.PlayerView

open MAG
open MAG.Events

type You =
    {
        Name : PlayerName
        Life : int
        Hand : Card list
        Stance : Card list
        Discards : Card list
    }

type Them =
    {
        Name : PlayerName
        Life : int
        Stance : Card list
        Discards : Card list
    }

type WaitingFor =
    | YouToPlayInitiative
    | YouToCounter of PlayerName * Card list
    | YouToAttack of PlayerName option
    | YouToMoveStance
    | Them of PlayerName * Card list option
    | ThemToMoveStance of PlayerName
    | ThemToPlayInitiative of PlayerName list

type OnGoing =
    {
        Turn : PlayerName option
        You : You
        Them : Them list
        WaitingFor : WaitingFor
    }

type Finished =
    {
        Winner : PlayerName
        You : You
        Them : Them list
    }
    
type CurrentState =
    | Nothing
    | OnGoing of OnGoing
    | Finished of Finished

// Serialization
open Chiron
open Chiron.Operators

type You with
    static member ToJson (y : You) =
        Json.write "name" y.Name
        *> Json.write "life" y.Life
        *> Json.write "hand" y.Hand
        *> Json.write "stance" y.Stance
        *> Json.write "discards" y.Discards
    static member FromJson (_ : You) =
            fun n l h s d ->
                {
                    Name = n
                    Life = l
                    Hand = h
                    Stance = s
                    Discards = d
                }
        <!> Json.read "name"
        <*> Json.read "life"
        <*> Json.read "hand"
        <*> Json.read "stance"
        <*> Json.read "discards"

type Them with
    static member ToJson (t : Them) =
        Json.write "name" t.Name
        *> Json.write "life" t.Life
        *> Json.write "stance" t.Stance
        *> Json.write "discards" t.Discards
    static member FromJson (_ : Them) =
            fun n l s d ->
                {
                    Name = n
                    Life = l
                    Stance = s
                    Discards = d
                }
        <!> Json.read "name"
        <*> Json.read "life"
        <*> Json.read "stance"
        <*> Json.read "discards"

open System
open System.Text.RegularExpressions

let (|Match|_|) (pat:string) (inp:string) =
    let m = Regex.Match(inp, pat, RegexOptions.Multiline)
    if m.Success
    then Some (List.tail [ for g in m.Groups -> g.Value ])
    else None

type WaitingFor with
    static member ToJson (w : WaitingFor) =
        match w with
        | YouToPlayInitiative ->
            Json.write "waiting" "You to play initiative"
        | YouToCounter (attacker, cards) ->
            Json.write "waiting" "You to counter"            
            *> Json.write "attacker" attacker
            *> Json.write "cards" cards
        | YouToAttack t ->
            Json.write "waiting" "You to attack"
            *> Json.write "target" t
        | YouToMoveStance ->
            Json.write "waiting" "You to move a card to stance"
        | Them (PlayerName player, cards) ->
            Json.write "waiting" (sprintf "%s to play" player)
            *> Json.write "cards" cards
        | ThemToPlayInitiative players ->
            Json.write "waiting" "For others to play initiative"
            *> Json.write "others" players
        | ThemToMoveStance player ->
            Json.write "waiting" "For them to move to stance"
            *> Json.write "player" player
    static member FromJson (_ : WaitingFor) =
        json {
            let! waiting = Json.read "waiting"
            match waiting with
            | "You to play initiative" ->
                return YouToPlayInitiative
            | "You to counter" ->
                let! cards = Json.read "cards"
                let! attacker = Json.read "attacker"
                return YouToCounter (attacker, cards)
            | "You to attack" ->
                let! target = Json.read "target"
                return YouToAttack target
            | "You to move a card to stance" ->
                return YouToMoveStance
            | Match "(.*) to play$" (x::[]) ->
                let! cards = Json.read "cards"
                return Them (PlayerName x, cards)
            | "For others to play initiative" ->
                let! others = Json.read "others"
                return ThemToPlayInitiative others
            | "For them to move to stance" ->
                let! player = Json.read "player"
                return ThemToMoveStance player
            | _ ->
                return! Json.error (sprintf "Unknown waiting for %s" waiting)
        }

type OnGoing with
    static member ToJson (o : OnGoing) =
        Json.write "you" o.You
        *> Json.write "them" o.Them
        *> Json.write "waitingFor" o.WaitingFor
        *> Json.write "turn" o.Turn
    static member FromJson (_ : OnGoing) =
            fun y t w p -> { You = y; Them = t; WaitingFor = w; Turn = p }
        <!> Json.read "you"
        <*> Json.read "them"
        <*> Json.read "waitingFor"
        <*> Json.read "turn"

type Finished with
    static member ToJson (o : Finished) =
        Json.write "you" o.You
        *> Json.write "them" o.Them
        *> Json.write "winner" o.Winner
    static member FromJson (_ : Finished) =
            fun y t w -> { You = y; Them = t; Winner = w }
        <!> Json.read "you"
        <*> Json.read "them"
        <*> Json.read "winner"

type CurrentState with
    static member ToJson (c : CurrentState) =
        match c with
        | Nothing ->
            Json.write "state" "nothing"
        | OnGoing o ->
            Json.write "state" "ongoing"
            *> Json.write "data" o
        | Finished f ->
            Json.write "state" "finished"
            *> Json.write "data" f
    static member FromJson (_ : CurrentState) =
        json {
            let! state = Json.read "state"
            match state with
            | "nothing" ->
                return Nothing
            | "ongoing" -> 
                let! data = Json.read "data"
                return OnGoing data
            | "finished" -> 
                let! data = Json.read "data"
                return Finished data
            | _ ->
                return! Json.error (sprintf "Unexpected state: %s" state)
        }
