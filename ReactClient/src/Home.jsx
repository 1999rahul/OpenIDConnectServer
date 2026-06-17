import { useAuth } from "react-oidc-context";
import { useState } from 'react';

// CallApiButton.tsx
function CallApiButton({ accessToken, setApiResponse }) {
    const callApi = async () => {
        try{
        const res = await fetch("https://localhost:7169/WeatherForecast", {
            headers: { Authorization: `Bearer ${accessToken}` },
        });
        setApiResponse(await res.json());
        }catch(err){
            setApiResponse(`Error: ${err.message}, Stack: ${err.stack}`);
            console.error("Error calling API:", err);
        }

    };

    return <button onClick={callApi}>Call API</button>;
}

function Home() {
    const auth = useAuth();
    const [apiResponse, setApiResponse] = useState(null);

    if (auth.isLoading) return <div>Loading...</div>;
    if (auth.error) return <div>Error: {auth.error.message}</div>;

    if (!auth.isAuthenticated) {
        return <button onClick={() => auth.signinRedirect()}>Log in</button>;
    }

    return (
        <div>
            <p>Hello, {auth.user?.profile.name}</p>
            <button onClick={() => auth.removeUser()}>Log out</button>
            <CallApiButton accessToken={auth.user?.access_token} setApiResponse={setApiResponse} />
            {apiResponse && (
                <div>
                    <h2>API Response:</h2>
                    <pre>{JSON.stringify(apiResponse, null, 2)}</pre>
                </div>
            )}
        </div>
    );
}



export default Home;