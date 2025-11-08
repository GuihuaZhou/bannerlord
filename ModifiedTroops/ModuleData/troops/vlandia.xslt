<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
<xsl:output omit-xml-declaration="yes"/>
<xsl:template match="@*|node()">
    <xsl:copy>
        <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
</xsl:template>
 
    <xsl:template match="NPCCharacter[@id='vlandian_recruit']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_footman']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_spearman']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_levy_crossbowman']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_crossbowman']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_billman']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_voulgier']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_pikeman']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_swordsman']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_sergeant']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_light_cavalry']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_cavalry']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_vanguard']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_hardened_crossbowman']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_sharpshooter']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_gallant']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_knight']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_champion']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_banner_knight']"/>
    <xsl:template match="NPCCharacter[@id='vlandian_squire']"/>

</xsl:stylesheet>
