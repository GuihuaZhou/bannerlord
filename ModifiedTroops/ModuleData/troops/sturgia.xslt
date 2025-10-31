<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
<xsl:output omit-xml-declaration="yes"/>
<xsl:template match="@*|node()">
    <xsl:copy>
        <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
</xsl:template>
 
    <xsl:template match="NPCCharacter[@id='sturgian_recruit']"/>
    <xsl:template match="NPCCharacter[@id='sturgian_warrior']"/>
    <xsl:template match="NPCCharacter[@id='sturgian_soldier']"/>
    <xsl:template match="NPCCharacter[@id='sturgian_woodsman']"/>
    <xsl:template match="NPCCharacter[@id='sturgian_brigand']"/>
    <xsl:template match="NPCCharacter[@id='sturgian_hunter']"/>
    <xsl:template match="NPCCharacter[@id='varyag_veteran']"/>
    <xsl:template match="NPCCharacter[@id='druzhinnik']"/>
    <xsl:template match="NPCCharacter[@id='druzhinnik_champion']"/>
    <xsl:template match="NPCCharacter[@id='sturgian_archer']"/>
    <xsl:template match="NPCCharacter[@id='sturgian_veteran_bowman']"/>
    <xsl:template match="NPCCharacter[@id='sturgian_hardened_brigand']"/>
    <xsl:template match="NPCCharacter[@id='sturgian_horse_raider']"/>
    <xsl:template match="NPCCharacter[@id='sturgian_berzerker']"/>
    <xsl:template match="NPCCharacter[@id='sturgian_ulfhednar']"/>


    <xsl:template match="NPCCharacter[@id='sturgian_spearman']"/>
    <xsl:template match="NPCCharacter[@id='sturgian_shock_troop']"/>
    <xsl:template match="NPCCharacter[@id='sturgian_veteran_warrior']"/>
    <xsl:template match="NPCCharacter[@id='sturgian_warrior_son']"/>
    <xsl:template match="NPCCharacter[@id='varyag']"/>

</xsl:stylesheet>
