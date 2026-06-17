import { useState } from 'react'
import reactLogo from './assets/react.svg'
import viteLogo from './assets/vite.svg'
import heroImg from './assets/hero.png'
import './App.css'
import { useAuth } from "react-oidc-context";
import { Routes, Route } from "react-router-dom";
import Home from "./Home";

function CallbackPage() {
    const auth = useAuth();

    if (auth.isLoading) return <div>Signing you in...</div>;
    if (auth.error) return <div>Auth error: {auth.error.message}</div>;

    // Once auth.isAuthenticated flips true, redirect away from /callback
    if (auth.isAuthenticated) {
        window.location.replace("/");
        return null;
    }

    return null;
}

function App() {
    return (
        <Routes>
            <Route path="/callback" element={<CallbackPage />} />
            <Route path="/" element={<Home />} />
        </Routes>
    );
}

export default App
