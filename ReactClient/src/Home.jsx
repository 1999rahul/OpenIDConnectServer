import { useAuth } from "react-oidc-context";

// CallApiButton.tsx
function CallApiButton({ accessToken }) {
    const callApi = async () => {
        try{
        const res = await fetch("https://localhost:7169/WeatherForecast", {
            headers: { Authorization: `Bearer ${accessToken}` },
        });
        console.log(await res.json());
        }catch(err){
            console.error("Error calling API:", err);
        }

    };

    return <button onClick={callApi}>Call API</button>;
}

function Home() {
    const auth = useAuth();

    if (auth.isLoading) return <div>Loading...</div>;
    if (auth.error) return <div>Error: {auth.error.message}</div>;

    if (!auth.isAuthenticated) {
        return <button onClick={() => auth.signinRedirect()}>Log in</button>;
    }

    return (
        <div>
            <p>Hello, {auth.user?.profile.name}</p>
            <button onClick={() => auth.removeUser()}>Log out</button>
            <CallApiButton accessToken={auth.user?.access_token} />
        </div>
    );
}



export default Home;