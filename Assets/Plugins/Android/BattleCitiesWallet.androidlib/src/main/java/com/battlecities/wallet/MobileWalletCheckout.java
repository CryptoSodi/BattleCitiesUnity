package com.battlecities.wallet;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.SystemClock;
import android.util.Base64;
import com.solana.mobilewalletadapter.clientlib.scenario.LocalAssociationIntentCreator;
import com.solana.mobilewalletadapter.clientlib.scenario.LocalAssociationScenario;
import java.time.Instant;
import java.util.Arrays;
import java.util.concurrent.FutureTask;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;
import java.util.concurrent.atomic.AtomicBoolean;
import org.json.JSONObject;

/** Signs the API's exact checkout message. Submission happens only after Unity saves its receipt. */
public final class MobileWalletCheckout {
    public interface Callback { void onComplete(String json); }
    public static Operation sign(Activity activity,String transaction,String wallet,String expiry,boolean psg1,String attempt,Callback callback) {
        Operation value=new Operation(activity,transaction,wallet,expiry,psg1,attempt,callback);
        new Thread(value::run,"BattleCities-CheckoutSigning").start();return value;
    }
    public static final class Operation {
        final Activity activity; final String encoded,wallet,expiry,attempt; final boolean psg1; final Callback callback;
        final AtomicBoolean done=new AtomicBoolean(); final long deadline=SystemClock.elapsedRealtime()+145000;
        volatile LocalAssociationScenario scenario;
        Operation(Activity activity,String encoded,String wallet,String expiry,boolean psg1,String attempt,Callback callback) {
            this.activity=activity;this.encoded=encoded;this.wallet=wallet;this.expiry=expiry;this.psg1=psg1;this.attempt=attempt;this.callback=callback;
        }
        long remaining() throws Exception {
            long value=deadline-SystemClock.elapsedRealtime();
            if(done.get()||value<=0)throw new TimeoutException();return value;
        }
        void run() {
            try {
                byte[] bytes=Base64.decode(encoded,Base64.DEFAULT);
                if(bytes.length<=65||bytes.length>1232||bytes[0]!=1||Instant.parse(expiry).toEpochMilli()<=System.currentTimeMillis())throw new Exception();
                scenario=new LocalAssociationScenario(120000);
                Intent intent=LocalAssociationIntentCreator.createAssociationIntent(null,scenario.getPort(),scenario.getSession());
                if(psg1)intent.setPackage("ag.jup.jupiter.android");
                if(intent.resolveActivity(activity.getPackageManager())==null)throw new Exception();
                var future=scenario.start();
                FutureTask<Void> launch=new FutureTask<>(()->{if(!done.get())activity.startActivity(intent);return null;});
                activity.runOnUiThread(launch);launch.get(Math.min(remaining(),10000),TimeUnit.MILLISECONDS);
                var client=future.get(Math.min(remaining(),35000),TimeUnit.MILLISECONDS);
                var auth=client.authorize(Uri.parse("https://battlecities.com"),null,"Battle Cities","solana:mainnet",null,null,null,null)
                    .get(Math.min(remaining(),120000),TimeUnit.MILLISECONDS);
                boolean matched=false;
                for(var account:auth.accounts)if(account.publicKey.length==32&&MobileWalletLogin.encodeBase58(account.publicKey).equals(wallet))matched=true;
                if(!matched){finish(false,null,"Select the same wallet used to sign in to Battle Cities.");return;}
                if(Instant.parse(expiry).toEpochMilli()<=System.currentTimeMillis()){finish(false,null,"The price quote expired. Request a fresh price.");return;}
                var signed=client.signTransactions(new byte[][]{bytes}).get(Math.min(remaining(),120000),TimeUnit.MILLISECONDS);
                if(signed.signedPayloads.length!=1)throw new Exception();
                byte[] result=signed.signedPayloads[0];
                if(result.length!=bytes.length||result[0]!=1||!Arrays.equals(Arrays.copyOfRange(bytes,65,bytes.length),Arrays.copyOfRange(result,65,result.length)))throw new Exception();
                remaining();finish(true,Base64.encodeToString(result,Base64.NO_WRAP),null);
            } catch(Exception error) {finish(false,null,"Wallet signing was cancelled or unavailable. No payment was submitted.");}
            finally {close();}
        }
        public void cancel(){done.set(true);close();}
        void close(){LocalAssociationScenario current=scenario;scenario=null;if(current!=null)current.close();}
        void finish(boolean ok,String transaction,String error) {
            if(!done.compareAndSet(false,true))return;
            try {JSONObject body=new JSONObject().put("attemptId",attempt).put("ok",ok);
                if(ok)body.put("signedTransaction",transaction);else body.put("error",error);callback.onComplete(body.toString());}
            catch(Exception ignored){}
        }
    }
}
