
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.jsx'
import { AuthProvider } from "react-oidc-context";
import { BrowserRouter } from "react-router-dom";

const oidcConfig = {
    authority: "https://localhost:44339/", // matches your issuer exactly, trailing slash included
    client_id: "react-spa1",
    redirect_uri: "https://localhost:52813/callback",
    post_logout_redirect_uri: "https://localhost:52813/",
    response_type: "code",
    scope: "openid profile email offline_access api",
    automaticSilentRenew: true,
    loadUserInfo: false, // userinfo_endpoint isn't implemented yet — avoid a failed call
};

createRoot(document.getElementById('root')).render(
    <AuthProvider {...oidcConfig}>
        <BrowserRouter>
            <App />
        </BrowserRouter>
    </AuthProvider>
)

